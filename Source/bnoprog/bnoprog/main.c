#define F_CPU 16000000UL

#include <avr/io.h>
#include <util/delay.h>
#include <stdlib.h>
#include <avr/interrupt.h>

struct bno_ver {
	uint8_t sw_lsb;
	uint8_t sw_msb;
	uint8_t bl;
};
typedef struct bno_ver bno_ver;

volatile uint8_t rx_buffer[256];
volatile uint8_t write_ptr = 0;

void string_to_serial(char* string);
void array_to_serial(uint8_t* array, uint8_t len);
void byte_to_serial(uint8_t data);

void twi_wait(void);

bno_ver bno_read_version(void);
void bno_reset(uint8_t boot);

void bno_read_reg(uint8_t addr, uint8_t len);
void bno_write_reg(uint8_t addr, uint8_t data);

uint8_t bno_update_write(volatile uint8_t* data, uint8_t len);

char text[50];
uint8_t text_index = 0;



int main(void)
{
	//uart
    UCSR0B |= (1 << RXCIE0) | (1 << RXEN0) | (1 << TXEN0);
	UCSR0C |= (1 << UCSZ01) | (1 << UCSZ00);
	UBRR0 = 3; //250k
	
	//i2c
	TWBR = 72; //100k, added robustness?
	
	
	bno_reset(0);
	
	
	
	bno_ver ver = bno_read_version();
		
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = ver.sw_msb;
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = ver.sw_lsb;
	

	bno_reset(1);
	
	sei();
		
	//wait for firmware size
	while((write_ptr) != 6) {}
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = bno_update_write(rx_buffer, 6);
	write_ptr = 0;
	
	
	//wait for packet size
	while((write_ptr) != 3) {}
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = bno_update_write(rx_buffer, 3);
	write_ptr = 0;
	
	
    while (1) 
    {
		static int16_t packet_counter = 0;
		
		if (packet_counter != 5099) {
			//wait for bulk of the packets
			while((write_ptr) != 32) {}
			while(!(UCSR0A & (1 << UDRE0)));
			UDR0 = bno_update_write(rx_buffer, 32);
			write_ptr = 0;
		} else {
			//last packet is smaller
			while((write_ptr) != 24) {}
			while(!(UCSR0A & (1 << UDRE0)));
			UDR0 = bno_update_write(rx_buffer, 24);
			write_ptr = 0;
		}
		packet_counter++;
    }
}

ISR (USART_RX_vect) {
	rx_buffer[write_ptr++] = UDR0;
}

void string_to_serial(char* string) {
	text_index = 0;
	while (string[text_index]) {
		while(!(UCSR0A & (1 << UDRE0)));
		UDR0 = string[text_index];
		text_index++;
	}
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = '\r';
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = '\n';
}

void array_to_serial(uint8_t* array, uint8_t len) {
	uint8_t array_index = 0;
	while(len) {
		while(!(UCSR0A & (1 << UDRE0)));
		UDR0 = array[array_index++];
		len--;
	}
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = '\r';
	while(!(UCSR0A & (1 << UDRE0)));
	UDR0 = '\n';
}

void byte_to_serial(uint8_t data) {
	text[0] = '0';
	text[1] = 'x';
	uint8_t offset = 2;
	if (data < 16) {
		text[2] = '0';
		offset = 3;
	}
	itoa(data, text + offset, 16);
	string_to_serial(text);
}

void twi_wait(void) {
	uint16_t cooldown = 1000;
	while (!(TWCR & (1 << TWINT)) && cooldown--) {
		_delay_us(100);
	}
}

bno_ver bno_read_version(void) {
	bno_ver out;
	
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1); //sla + w
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWDR = 4;
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1) | 1; //sla + r
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWCR = (1 << TWINT) | (1 << TWEA) | (1 << TWEN);
	twi_wait();
	out.sw_lsb = TWDR;
	
	TWCR = (1 << TWINT) | (1 << TWEA) | (1 << TWEN);
	twi_wait();
	out.sw_msb = TWDR;
	
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	out.bl = TWDR;
	
	TWCR = (1 << TWINT) | (1 << TWSTO) | (1 << TWEN);
	
	return out;
}

void bno_reset(uint8_t boot) {
	PORTB &= ~((1 << 1) | (1 << 0)); //reset a boot open drain!!
	if (boot) {
		DDRB |= (1 << 0);
	} else {
		DDRB &= ~(1 << 0);
	}
	DDRB |= (1 << 1);
	_delay_ms(10);
	DDRB &= ~(1 << 1);
	_delay_ms(1000);
}

void bno_read_reg(uint8_t addr, uint8_t len) {
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1); //sla + w
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWDR = addr;
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1) | 1; //sla + r
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	for (uint8_t i = 0; i < len - 1; i++) {
		TWCR = (1 << TWINT) | (1 << TWEA) | (1 << TWEN);
		twi_wait();
		byte_to_serial(TWDR);
	}
	
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	byte_to_serial(TWDR);
	
	TWCR = (1 << TWINT) | (1 << TWSTO) | (1 << TWEN);
}

void bno_write_reg(uint8_t addr, uint8_t data) {
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1); //sla + w
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWDR = addr;
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWDR = data;
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();

	TWCR = (1 << TWINT) | (1 << TWSTO) | (1 << TWEN);
}

uint8_t bno_update_write(volatile uint8_t* data, uint8_t len) {
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1); //sla + w
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	for (uint8_t i = 0; i < len; i++) {
		TWDR = data[i];
		TWCR = (1 << TWINT) | (1 << TWEN);
		twi_wait();
	}
	
	TWCR = (1 << TWINT) | (1 << TWSTA) | (1 << TWEN);
	twi_wait();
	
	TWDR = (0x29 << 1) | 1; //sla + r
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	
	TWCR = (1 << TWINT) | (1 << TWEN);
	twi_wait();
	register uint8_t resp = TWDR;
	
	TWCR = (1 << TWINT) | (1 << TWSTO) | (1 << TWEN);
	
	return resp;
}