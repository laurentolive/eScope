/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2026 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include <math.h>
#include "eScopeProtocol.h"
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/

COM_InitTypeDef BspCOMInit;

/* USER CODE BEGIN PV */
/* Variables to watch in eScope (names looked up in the .elf / .map) */
volatile uint16_t u16Counter;          /* +1 every sampling period             */
volatile float    fAmplitude = 10.0f;  /* writable from the eScope Watch window */
volatile float    fFrequency = 1.0f;   /* Hz, writable too                      */
volatile float    fSine;               /* fAmplitude * sin(2 pi fFrequency t)   */
volatile float    fSquare;             /* square wave, same frequency           */
volatile uint8_t  u8Button;            /* 1 while the blue USER button is held  */

UART_HandleTypeDef huart1;             /* USART1 PB6/PB7, ESCOPE_UART_BRIDGE    */

static uint8_t              tu8eScopeCfg[ESCOPE_PROTOCOL_EEPROM_SIZE];
static eScopeProtocolCfg_t  sProtoCfg = { .pu8Cfg = tu8eScopeCfg };
static eScopeProtocol_t     sProto;
/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);
/* USER CODE BEGIN PFP */
#if defined(ESCOPE_TRANSPORT_UART)
static void MX_USART1_UART_Init(void);
#endif
static void App_Update(void);
/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */
/* Demo signals, computed every ESCOPE_SAMPLING_MS */
static void App_Update(void)
{
  static float fPhase = 0.0f;

  u16Counter++;
  fPhase += fFrequency * (float)ESCOPE_SAMPLING_MS / 1000.0f;
  if (fPhase >= 1.0f) fPhase -= 1.0f;
  fSine   = fAmplitude * sinf(2.0f * 3.14159265f * fPhase);
  fSquare = (fPhase < 0.5f) ? fAmplitude : -fAmplitude;
  u8Button = (BSP_PB_GetState(BUTTON_USER) == 0) ? 1U : 0U;   /* active low */
}
/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  /* USER CODE BEGIN 2 */

  /* USER CODE END 2 */

  /* Initialize leds */
  BSP_LED_Init(LED_GREEN);

  /* Initialize USER push-button, will be used to trigger an interrupt each time it's pressed.*/
  BSP_PB_Init(BUTTON_USER, BUTTON_MODE_EXTI);

  /* Initialize COM1 port (115200, 8 bits (7-bit data + 1 stop bit), no parity */
  BspCOMInit.BaudRate   = 115200;
  BspCOMInit.WordLength = COM_WORDLENGTH_8B;
  BspCOMInit.StopBits   = COM_STOPBITS_1;
  BspCOMInit.Parity     = COM_PARITY_NONE;
  BspCOMInit.HwFlowCtl  = COM_HWCONTROL_NONE;
  if (BSP_COM_Init(COM1, &BspCOMInit) != BSP_ERROR_NONE)
  {
    Error_Handler();
  }

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */

  /* eScope: UART (ST-LINK virtual COM port or USART1 bridge) or RTT, see eScopeCfg.h */
#if defined(ESCOPE_TRANSPORT_UART)
  if (ESCOPE_UART_ID == ESCOPE_UART_BRIDGE)
  {
    MX_USART1_UART_Init();
  }
  eScopeTransportCfg_t sTransCfg = { .u8Id = ESCOPE_UART_ID };
#else
  eScopeTransportCfg_t sTransCfg = { .u8Id = 0U };   /* RTT channel 0 */
#endif
  eScopeProtocol_Init(&sProto, &sTransCfg, &sProtoCfg);

  uint32_t u32LastTick = HAL_GetTick();
  while (1)
  {
    eScopeProtocol_Poll(&sProto);                       /* RTT: drains the down buffer */

    if ((HAL_GetTick() - u32LastTick) >= ESCOPE_SAMPLING_MS)
    {
      u32LastTick += ESCOPE_SAMPLING_MS;
      App_Update();
      eScopeProtocol_Process(&sProto);                  /* sends one frame */
    }
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};

  __HAL_FLASH_SET_LATENCY(FLASH_LATENCY_0);

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSI;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.HSIDiv = RCC_HSI_DIV4;
  RCC_OscInitStruct.HSICalibrationValue = RCC_HSICALIBRATION_DEFAULT;
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_HSI;
  RCC_ClkInitStruct.SYSCLKDivider = RCC_SYSCLK_DIV1;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_HCLK_DIV1;
  RCC_ClkInitStruct.APB1CLKDivider = RCC_APB1_DIV1;

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_0) != HAL_OK)
  {
    Error_Handler();
  }
}

/**
  * @brief GPIO Initialization Function
  * @param None
  * @retval None
  */
static void MX_GPIO_Init(void)
{
  /* USER CODE BEGIN MX_GPIO_Init_1 */

  /* USER CODE END MX_GPIO_Init_1 */

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOC_CLK_ENABLE();
  __HAL_RCC_GPIOF_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();

  /* USER CODE BEGIN MX_GPIO_Init_2 */

  /* USER CODE END MX_GPIO_Init_2 */
}

/* USER CODE BEGIN 4 */
#if defined(ESCOPE_TRANSPORT_UART)
/* USART1 on PB6 (TX) / PB7 (RX), 8N1: link to a Bluetooth or Wi-Fi bridge */
static void MX_USART1_UART_Init(void)
{
  huart1.Instance            = USART1;
  huart1.Init.BaudRate       = ESCOPE_PROTOCOL_BAUDRATE;
  huart1.Init.WordLength     = UART_WORDLENGTH_8B;
  huart1.Init.StopBits       = UART_STOPBITS_1;
  huart1.Init.Parity         = UART_PARITY_NONE;
  huart1.Init.Mode           = UART_MODE_TX_RX;
  huart1.Init.HwFlowCtl      = UART_HWCONTROL_NONE;
  huart1.Init.OverSampling   = UART_OVERSAMPLING_16;
  huart1.Init.OneBitSampling = UART_ONE_BIT_SAMPLE_DISABLE;
  huart1.Init.ClockPrescaler = UART_PRESCALER_DIV1;
  if (HAL_UART_Init(&huart1) != HAL_OK)
  {
    Error_Handler();
  }
}

void eScopeTransportUartStm32_RxCallback(eScopeTransport_t* pThis, UART_HandleTypeDef* huart);

void HAL_UART_RxCpltCallback(UART_HandleTypeDef* huart)
{
  eScopeTransportUartStm32_RxCallback(&sProto.sTransport, huart);
}
#endif
/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}
#ifdef USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
