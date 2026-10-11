/*
* Copyright (c) 2020 - 2024 Renesas Electronics Corporation and/or its affiliates
*
* SPDX-License-Identifier: BSD-3-Clause
*/

#include "hal_data.h"
#include "eScopeProtocol.h"

void R_BSP_WarmStart(bsp_warm_start_event_t event);

extern bsp_leds_t g_bsp_leds;

/* eScope state ---------------------------------------------------------------*/
static eScopeProtocolCfg_t  sProtoCfg;   /* cfg buffer is inline in V3ID */
static eScopeProtocol_t     sProto;

/* Variables exposed to eScope ------------------------------------------------*/
volatile uint32_t u32Counter;
volatile float    fSineApprox;   /* example: updated each iteration */

/*******************************************************************************************************************//**
 * @brief  Application entry point — blinky + eScope over SEGGER RTT
 *
 * eScope transport : SEGGER RTT channel 0, or UART SCI9 (see eScopeCfg.h)
 * eScope protocol  : V3ID
 *
 * Scope loop, every ESCOPE_SAMPLING_MS: eScopeProtocol_Poll() drains RTT RX, then
 * eScopeProtocol_Process() sends the configured channel data back to the host.
 **********************************************************************************************************************/
void hal_entry (void)
{
#if BSP_TZ_SECURE_BUILD
    R_BSP_NonSecureEnter();
#endif

    /* Configure pins */
    bsp_leds_t leds = g_bsp_leds;

    /* eScope init — RTT channel 0 / UART g_uart9 */
    eScopeTransportCfg_t sTransCfg = { .u8Id = 0U };
    eScopeProtocol_Init(&sProto, &sTransCfg, &sProtoCfg);

    uint32_t u32LedTick = 0U;
    bsp_io_level_t pin_level = BSP_IO_LEVEL_LOW;

    while (1)
    {
        /* eScope — poll RTT then process one frame */
        eScopeProtocol_Poll(&sProto);
        eScopeProtocol_Process(&sProto);

        /* Application logic */
        u32Counter++;
        fSineApprox = 10.0f * sinf(2.0f * 3.14159f * 1.0f * ((float)(u32Counter * ESCOPE_SAMPLING_MS))/1000);  // 1Hz, ±10

        /* LED blink — toggle every 500 ms */
        if (++u32LedTick >= (500U / ESCOPE_SAMPLING_MS))
        {
            u32LedTick  = 0U;
            pin_level   = (BSP_IO_LEVEL_LOW == pin_level) ? BSP_IO_LEVEL_HIGH : BSP_IO_LEVEL_LOW;

            if (leds.led_count > 0U)
            {
                R_BSP_PinAccessEnable();
                R_BSP_PinWrite((bsp_io_port_pin_t)leds.p_leds[0], pin_level);
                R_BSP_PinAccessDisable();
            }
        }

        R_BSP_SoftwareDelay(ESCOPE_SAMPLING_MS, BSP_DELAY_UNITS_MILLISECONDS);
    }
}

/*******************************************************************************************************************//**
 * Called at various points during startup.  Pins are configured before main().
 **********************************************************************************************************************/
void R_BSP_WarmStart (bsp_warm_start_event_t event)
{
    if (BSP_WARM_START_RESET == event)
    {
#if BSP_FEATURE_FLASH_LP_VERSION != 0
        R_FACI_LP->DFLCTL = 1U;
#endif
    }

    if (BSP_WARM_START_POST_C == event)
    {
        R_IOPORT_Open(&IOPORT_CFG_CTRL, &IOPORT_CFG_NAME);
    }
}
