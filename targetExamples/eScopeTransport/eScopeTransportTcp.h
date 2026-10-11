/*******************************************************************************
 *
 *              #####   File : eScopeTransportTcp.h
 *              #####
 *              #####   TCP transport — concrete types only
 *              #####
 *      #############   Do not include this file directly from protocol modules.
 *   ################   Include eScopeTransport.h instead.
 *  ######     ######
 *  ######      #####
 *  *##########
 *    ##############
 *         ###########
 *    ##         ######
 *  ######        #####
 *  ######       ######
 *
 ******************************************************************************/

#ifndef ESCOPE_TRANSPORT_TCP_H_
#define ESCOPE_TRANSPORT_TCP_H_

#include <stdint.h>
#include <stdbool.h>

/*******************************************************************************
 * TCP transport handle
 ******************************************************************************/

typedef struct {
    int   fd;               /* socket file descriptor (-1 = not connected) */
    void (*pfCB)(uint32_t event, void* pvCtxt); /* set by eScopeTransport_Init */
    void* pvCtxt;
} eScopeTransportTcp_t;

/*******************************************************************************
 * TCP init configuration
 ******************************************************************************/

typedef struct {
    uint32_t u32IpAddr;     /* server IP, host byte order (e.g. 0xC0A80001) */
    uint16_t u16Port;       /* server port                                   */
} eScopeTransportTcp_Cfg_t;

#endif /* ESCOPE_TRANSPORT_TCP_H_ */
