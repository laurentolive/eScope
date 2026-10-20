/*******************************************************************************
 *
 *              #####   File : eScopeDep.h
 *              #####
 *              #####   Dependencies — firmware simulated on PC
 *              #####
 *      #############   The simulator calls the firmware (tick, UART reception)
 *   ################   from its scheduler thread only: no critical section.
 *
 ******************************************************************************/

#ifndef ESCOPE_DEP_H_
#define ESCOPE_DEP_H_

#define CRITICAL_SECTION_ALLOC()
#define CRITICAL_SECTION_ENTER()
#define CRITICAL_SECTION_EXIT()

#endif /* ESCOPE_DEP_H_ */
