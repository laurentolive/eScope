/*******************************************************************************
 *
 *              #####   File : eScopeCfg.h
 *              #####
 *              #####   eScope project configuration — firmware simulated on PC
 *              #####
 *      #############   Protocol  : V3, 64 bits addresses (x64 DLL)
 *   ################   Transport : UART of the simulated board, relayed to TCP
 *  ######     ######               by the UART driver of the simulator
 *  ######      #####
 *  *##########         The DLL may be relocated when loaded (ASLR): addresses
 *    ##############    received from eScope are link addresses (map / ELF),
 *         ###########  translated by ESCOPE_ADDR_TO_PTR.
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#ifndef ESCOPE_CFG_H_
#define ESCOPE_CFG_H_

#include <stdint.h>
#include <stdbool.h>

/*******************************************************************************
 * Protocol version and transport
 ******************************************************************************/

#define ESCOPE_PROTOCOL_V3
#define ESCOPE_TRANSPORT_UART
#define ESCOPE_PROTOCOL_BAUDRATE 115200U  /* not limiting: the simulated UART has no baudrate */

/*******************************************************************************
 * Protocol parameters
 ******************************************************************************/

#define ESCOPE_PROTOCOL_CHAN_MAX      (16U)
#if defined(_WIN64) || defined(__x86_64__)
    #define ESCOPE_PROTOCOL_ADDRESS_SIZE  (8U)   /* eScope device: "Addresses: 64 bits" */
#else
    #define ESCOPE_PROTOCOL_ADDRESS_SIZE  (4U)   /* x86 DLL: 32 bits */
#endif
#define ESCOPE_PROTOCOL_DATASIZE_SIZE (2U)
#define ESCOPE_PROTOCOL_TX_SIZE       (250U)

/*******************************************************************************
 * Target memory access
 ******************************************************************************/

/* link base of the DLL: LINK_BASE of the Makefile (--image-base), given with -D */
#ifndef SIM_LINK_BASE
    #error "SIM_LINK_BASE: link base of the DLL, defined by the Makefile"
#endif

/* link address -> loaded address: __ImageBase is the base the DLL is loaded at */
extern char __ImageBase;
#define ESCOPE_ADDR_TO_PTR(addr) \
    ((uint8_t*)((uintptr_t)&__ImageBase + (uintptr_t)((addr) - SIM_LINK_BASE)))

/* sanity check: an access outside the DLL image is refused (ERR_ADDR) instead of
 * crashing the simulator */
bool simHw_IsValid(const uint8_t* pu8, uint32_t u32Size);
#define ESCOPE_ADDR_IS_VALID(ptr, size)  simHw_IsValid((ptr), (size))

/*******************************************************************************
 * Sampling period (ms) = period of eScopeProtocol_Process() in app.c, in
 * simulated time. Enter the same value as "Sampling Time" in the eScope device.
 ******************************************************************************/

#define ESCOPE_SAMPLING_MS  (1U)

#endif /* ESCOPE_CFG_H_ */
