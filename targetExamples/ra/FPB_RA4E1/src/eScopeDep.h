/*******************************************************************************
 *
 *              #####   File : eScopeDep.h
 *              #####
 *              #####   eScope platform dependencies — FPB-RA4E1 (Cortex-M33)
 *              #####
 *      #############
 *   ################
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

#ifndef ESCOPE_DEP_H_
#define ESCOPE_DEP_H_

#include <stdint.h>
#include <assert.h>

__attribute__((always_inline)) static inline uint32_t __eScope_get_PRIMASK(void)
{
    uint32_t result;
    __asm volatile ("MRS %0, primask" : "=r" (result));
    return result;
}

__attribute__((always_inline)) static inline void __eScope_set_PRIMASK(uint32_t priMask)
{
    __asm volatile ("MSR primask, %0" : : "r" (priMask) : "memory");
}

#define CRITICAL_SECTION_ALLOC() uint32_t old_mask_level = 0U
#define CRITICAL_SECTION_ENTER() do { old_mask_level = __eScope_get_PRIMASK(); __eScope_set_PRIMASK(1U); } while (0)
#define CRITICAL_SECTION_EXIT()  do { __eScope_set_PRIMASK(old_mask_level); } while (0)

#define ASSERT assert

#endif /* ESCOPE_DEP_H_ */
