/*******************************************************************************
 *
 *              #####   File : simHw.h
 *              #####
 *              #####   Simulated board: interface between the firmware DLL
 *              #####   and the simulator (host)
 *      #############
 *   ################   Host -> firmware (exported by the DLL), called from the
 *  ######     ######   scheduler thread of the simulator only:
 *  ######      #####     sim_Init     once, gives the host callbacks
 *  *##########           sim_Tick     1 ms timer interrupt (simulated time)
 *    ##############      sim_UartRx   UART reception interrupt, one byte
 *         ###########  Firmware -> host: callbacks of simHost_t.
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#ifndef SIM_HW_H_
#define SIM_HW_H_

#include <stdint.h>

#ifdef _WIN32
    #define SIM_API __declspec(dllexport)
#else
    #define SIM_API __attribute__((visibility("default")))
#endif

#define SIM_UART_ESCOPE (0U)   /* UART of the board used by eScope */

/* UART transmission of the firmware: u16Len bytes on UART u8Id */
typedef void (*simUartTx_t)(uint8_t u8Id, const uint8_t* pu8Data, uint16_t u16Len);

SIM_API void sim_Init(simUartTx_t pfUartTx);
SIM_API void sim_Tick(void);
SIM_API void sim_UartRx(uint8_t u8Id, uint8_t u8Byte);

/* firmware application (app.c) */
void app_Init(void);
void app_Tick(void);

#endif /* SIM_HW_H_ */
