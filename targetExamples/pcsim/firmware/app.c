/*******************************************************************************
 *
 *              #####   File : app.c
 *              #####
 *              #####   Firmware application, as on the target: the same code
 *              #####   runs on the simulated board
 *      #############
 *   ################   Variables (same as tools/automation/sim/sim_v3.py):
 *  ######     ######     motor_speed   int16   1000 * sin(2 pi 50 Hz t)
 *  ######      #####     motor_current int16   motor_cfg * 10 + 5 * sin(2 pi 5 Hz t)
 *  *##########           motor_cfg     uint32  written by eScope (default 7)
 *    ##############      led_state     uint16  10 Hz square, duty 30 % (0 / 100)
 *         ###########
 *    ##         ######   t: simulated time (ticks of the simulator)
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#include <math.h>
#include "eScopeProtocol.h"
#include "simHw.h"

#define PI (3.14159265358979)

volatile int16_t  motor_speed;
volatile int16_t  motor_current;
volatile uint32_t motor_cfg = 7U;
volatile uint16_t led_state;

static eScopeProtocol_t sProto;
static uint8_t          tu8ProtoCfg[ESCOPE_PROTOCOL_EEPROM_SIZE];
static uint32_t         u32Tick;

void app_Init(void)
{
    eScopeProtocolCfg_t  sProtoCfg = { tu8ProtoCfg };
    eScopeTransportCfg_t sTransCfg = { SIM_UART_ESCOPE };

    eScopeProtocol_Init(&sProto, &sTransCfg, &sProtoCfg);
}

/* 1 ms timer interrupt */
void app_Tick(void)
{
    double t = u32Tick / 1000.0;

    motor_speed   = (int16_t)lround(1000.0 * sin(2.0 * PI * 50.0 * t));
    motor_current = (int16_t)lround(motor_cfg * 10.0 + 5.0 * sin(2.0 * PI * 5.0 * t));
    led_state     = ((u32Tick % 100U) < 30U) ? 100U : 0U;

    if ((u32Tick % ESCOPE_SAMPLING_MS) == 0U)
    {
        eScopeProtocol_Process(&sProto);   /* sampling point, chosen by the user */
    }
    u32Tick++;
}
