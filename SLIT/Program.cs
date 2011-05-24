/*
Copyright (c) 2011, Valentin Lorentz
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice,
this list of conditions, and the following disclaimer.
* Redistributions in binary form must reproduce the above copyright notice,
this list of conditions, and the following disclaimer in the
documentation and/or other materials provided with the distribution.
* Neither the name of the author of this software nor the name of
contributors to this software may be used to endorse or promote products
derived from this software without specific prior written consent.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
POSSIBILITY OF SUCH DAMAGE.

*/

using System;
using Microsoft.SPOT;
using System.Threading;
using Microsoft.SPOT.Hardware;
using GHIElectronics.NETMF.FEZ;

namespace MFConsoleApplication1
{
    public class Program
    {
        /***********************************************************************
         * tests related stuff
        ***********************************************************************/
        const bool TEST = false;
        static string testLogs = "";
        static int testErrorCount = 0;

        /***********************************************************************
         * "Classic" attributes
        ***********************************************************************/
        static Thread threadSetToPaper;
        static Thread threadSetToPlastic;
        static Thread topMotorThread;
        static Thread sortMotorThread;
        static Thread soundResetThread;
        static short soundCurrentId = -1;

        /***********************************************************************
         * Constants
        ***********************************************************************/
        // Constants used for arrays
        const short PLASTIC = 0;
        const short METAL = 1;
        const short PAPER = 2;
        const short NUMBER_OF_BUTTONS = 3;

        const short SENSOR_SORT = 0;
        const short SENSOR_TOP = 1;
        const short SENSOR_INDUCTIVE = 2;
        const short NUMBER_OF_SENSORS = 3;

        // Constants used for motor events
        const short MOTOR_TOP = 0;
        const short MOTOR_SORT = 1;
        const short NUMBER_OF_MOTORS = 2;

        const short MOTOR_RIGHT = 0;
        const short MOTOR_LEFT = 1;
        const short MOTOR_STOP = 2;
        const short NUMBER_OF_PORTS_PER_MOTOR = 2;

        // Constants used for sound system
        const short SOUND_PLAY = 0;
        const short SOUND_NEXT = 1;
        const short SOUND_RESET = 2;
        const short NUMBER_OF_SOUND_CONTROLS = 3;

        // Ports
        static InterruptPort[] buttons = new InterruptPort[NUMBER_OF_BUTTONS];
        static InterruptPort[] sensors = new InterruptPort[NUMBER_OF_SENSORS];
        static OutputPort[][] motors = new OutputPort[NUMBER_OF_MOTORS][];
        static OutputPort[] sound = new OutputPort[NUMBER_OF_SOUND_CONTROLS];

        // Sounds
        static short SOUND_NONE = -1;
        static short SOUND_WAITING = 0;
        static short SOUND_OK = 1;
        static short SOUND_ERROR = 2;
        static short SOUND_NOT_YET = 3; // Shouldn't have pressed a button yet
        static short[] SOUND_DURATION = { 2900, 2150, 2800, 3500 };

        // Internal state stuff
        const short INTERNAL_SLEEPING = 0;
        const short INTERNAL_FALLING = 1;
        const short INTERNAL_PUT_ON_SORT_BOARD = 2;
        const short INTERNAL_WAITING_FOR_BUTTON = 3;
        static short currentState = INTERNAL_SLEEPING;
        static short objectType = -1;

        // Timeouts (in milliseconds)
        static int TIMEOUT = 1000;
        static int TIME_OPEN_MOTOR_TOP = 500;
        static int TIME_CLOSE_MOTOR_TOP = 600;
        static int TIME_PLASTIC_DOWN = 500;
        static int TIME_PLASTIC_UP = 500;
        static int TIME_METAL_DOWN = 500;
        static int TIME_METAL_UP = 500;
        static int TIMEOUT_DETECT_AS_PAPER = 10000; // If the top board opened, and this timer ends without any other event, we consider the object type is PLASTIC
        static int TIMEOUT_DETECT_AS_PLASTIC = 3000;
        static int DELAY_SOUND_SIGNAL = 150;

        /***********************************************************************
         * Initialization
        ***********************************************************************/
        public static void Main()
        {
            OutputPort LED;
            LED = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, true);
            for (int i=0; i<0*2; i++)
            {
                LED.Write(!LED.Read());
                Thread.Sleep(100);
            }
            if (TEST)
                test();
            else
                initializePorts();
            
            Thread.Sleep(Timeout.Infinite);
        }
        public static void initializePorts()
        {
            // Method called at startup

            // Change the anti-bounce filter delay (defaults to ~200ms)
            Microsoft.SPOT.Hardware.Cpu.GlitchFilterTime = new TimeSpan(0, 0, 0, 0, 400);

            // Initialize the buttons
            buttons[PLASTIC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di4, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            buttons[METAL] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di5, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            buttons[PAPER] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di6, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);

            // Initialize the button interruptions
            buttons[PLASTIC].OnInterrupt += new NativeEventHandler(onButtonPlastic);
            buttons[METAL].OnInterrupt += new NativeEventHandler(onButtonMetal);
            buttons[PAPER].OnInterrupt += new NativeEventHandler(onButtonPaper);


            // Initialize the sensors
            sensors[SENSOR_SORT] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di0, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            sensors[SENSOR_TOP] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di2, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);
            sensors[SENSOR_INDUCTIVE] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di12, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeLow);

            // Initialize the sensors interruptions:
            sensors[SENSOR_SORT].OnInterrupt += new NativeEventHandler(onSensorSort);
            sensors[SENSOR_TOP].OnInterrupt += new NativeEventHandler(onSensorTop);
            sensors[SENSOR_INDUCTIVE].OnInterrupt += new NativeEventHandler(onSensorInductive);

            // Initialize the motors
            motors[MOTOR_TOP] = new OutputPort[NUMBER_OF_PORTS_PER_MOTOR];
            motors[MOTOR_SORT] = new OutputPort[NUMBER_OF_PORTS_PER_MOTOR];
            motors[MOTOR_SORT][MOTOR_LEFT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di8, false);
            motors[MOTOR_SORT][MOTOR_RIGHT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di9, false);
            motors[MOTOR_TOP][MOTOR_LEFT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di11, false);
            motors[MOTOR_TOP][MOTOR_RIGHT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di10, false);

            // Initialize the sound controls
            sound[SOUND_PLAY] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An1, true);
            sound[SOUND_NEXT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An2, true);
            sound[SOUND_RESET] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.An3, true);
            sound[SOUND_RESET].Write(false);
            Thread.Sleep(DELAY_SOUND_SIGNAL);
            sound[SOUND_RESET].Write(true);
        }

        /***********************************************************************
         * Buttons handling
        ***********************************************************************/
        public static void onButtonPlastic(uint port, uint state, DateTime time) { onButton(PLASTIC); }
        public static void onButtonMetal(uint port, uint state, DateTime time) { onButton(METAL); }
        public static void onButtonPaper(uint port, uint state, DateTime time) { onButton(PAPER); }
        public static void onButton(short id)
        {
            if (currentState == INTERNAL_WAITING_FOR_BUTTON)
            {
                if (id == objectType)
                {
                    playSound(SOUND_OK);
                    currentState = INTERNAL_SLEEPING;
                }
                else
                    playSound(SOUND_ERROR);
            }
            else
                playSound(SOUND_NOT_YET);
        }

        /***********************************************************************
         * Sound handling
        ***********************************************************************/
        public static void playSound(short id)
        {
            while (soundCurrentId != SOUND_NONE)
                Thread.Sleep(100);
            Debug.Print("Appel avec " + id.ToString());

            sound[SOUND_RESET].Write(false);
            Thread.Sleep(DELAY_SOUND_SIGNAL);
            sound[SOUND_RESET].Write(true);
            Thread.Sleep(DELAY_SOUND_SIGNAL);

            sound[SOUND_PLAY].Write(false);
            Thread.Sleep(DELAY_SOUND_SIGNAL);
            sound[SOUND_PLAY].Write(true);
            for (short i = 0; i < id; i++)
            {
                sound[SOUND_NEXT].Write(false);
                Thread.Sleep(DELAY_SOUND_SIGNAL);
                sound[SOUND_NEXT].Write(true);
                Thread.Sleep(DELAY_SOUND_SIGNAL);
            }

            soundCurrentId = id;
            Thread.Sleep(SOUND_DURATION[soundCurrentId]);
            sound[SOUND_RESET].Write(false);
            Thread.Sleep(DELAY_SOUND_SIGNAL);
            sound[SOUND_RESET].Write(true);
            Thread.Sleep(DELAY_SOUND_SIGNAL);
            soundCurrentId = SOUND_NONE;

            if (TEST)
            {
                if (id == SOUND_OK)
                    testLog("Sound: ok");
                else if (id == SOUND_NOT_YET)
                    testLog("Sound: not yet");
                else if (id == SOUND_ERROR)
                    testLog("Sound: error");
                else if (id == SOUND_WAITING)
                    testLog("Sound: waiting");
                else
                    testLog("Sound: unknown");
            }
        }
        /***********************************************************************
         * Sensors handling
        ***********************************************************************/
        public static void onSensorTop(uint port, uint state, DateTime time)
        {
            if (currentState == INTERNAL_WAITING_FOR_BUTTON)
                currentState = INTERNAL_SLEEPING; // Some bad boy did not press the button
            if (currentState != INTERNAL_SLEEPING)
                return;
            currentState = INTERNAL_FALLING;
            setToPaperIfDetectionTimesOut();
            if (TEST)
                testLog("Motor: top, left, right, stop");
            else
            {
                try
                {
                    if (topMotorThread != null)
                    {
                        topMotorThread.Abort();
                        //topMotorThread = null;
                    }
                }
                catch (Exception) { }
                topMotorThread = new Thread(_onSensorTop);
                topMotorThread.Start();
            }
        }
        public static void _onSensorTop()
        {
            changeMotorStatus(MOTOR_TOP, MOTOR_LEFT);
            Thread.Sleep(TIME_OPEN_MOTOR_TOP);
            changeMotorStatus(MOTOR_TOP, MOTOR_RIGHT);
            Thread.Sleep(TIME_CLOSE_MOTOR_TOP);
            changeMotorStatus(MOTOR_TOP, MOTOR_STOP);
        }
        public static void onSensorSort(uint port, uint state, DateTime time)
        {
            if (currentState != INTERNAL_FALLING)
                return;
            currentState = INTERNAL_PUT_ON_SORT_BOARD;
            setToPlasticIfDetectionTimesOut();
        }
        public static void onSensorInductive(uint port, uint state, DateTime time)
        {
            if (currentState != INTERNAL_PUT_ON_SORT_BOARD && currentState != INTERNAL_FALLING)
                return;
            typeDetected(METAL);
            currentState = INTERNAL_WAITING_FOR_BUTTON;
            if (TEST)
                testLog("Motor: sort, left, right, stop");
            sortMotorThread = new Thread(_onSensorInductive);
            sortMotorThread.Start();
        }
        public static void _onSensorInductive()
        {
            changeMotorStatus(MOTOR_SORT, MOTOR_LEFT);
            Thread.Sleep(TIME_METAL_DOWN);
            changeMotorStatus(MOTOR_SORT, MOTOR_RIGHT);
            Thread.Sleep(TIME_METAL_UP);
            changeMotorStatus(MOTOR_SORT, MOTOR_STOP);
        }

        /***********************************************************************
         * Plastic detection (by timeout)
        ***********************************************************************/
        public static void setToPlasticIfDetectionTimesOut()
        {
            threadSetToPlastic = new Thread(_setToPlasticIfDetectionTimesOut);
            threadSetToPlastic.Start();
        }
        public static void _setToPlasticIfDetectionTimesOut()
        {
            Thread.Sleep(TIMEOUT_DETECT_AS_PLASTIC);
            if (currentState != INTERNAL_PUT_ON_SORT_BOARD)
                return;
            typeDetected(PLASTIC);
            currentState = INTERNAL_WAITING_FOR_BUTTON;
            if (TEST)
                testLog("Motor: sort, right, left, stop");

            changeMotorStatus(MOTOR_SORT, MOTOR_RIGHT);
            Thread.Sleep(TIME_PLASTIC_DOWN);
            changeMotorStatus(MOTOR_SORT, MOTOR_LEFT);
            Thread.Sleep(TIME_PLASTIC_UP);
            changeMotorStatus(MOTOR_SORT, MOTOR_STOP);

            threadSetToPlastic = null;
        }

        /***********************************************************************
         * Paper detection (by timeout)
        ***********************************************************************/
        public static void setToPaperIfDetectionTimesOut()
        {
            threadSetToPaper = new Thread(_setToPaperIfDetectionTimesOut);
            threadSetToPaper.Start();
        }
        public static void _setToPaperIfDetectionTimesOut()
        {
            Thread.Sleep(TIMEOUT_DETECT_AS_PAPER);
            if (currentState == INTERNAL_FALLING)
            {
                typeDetected(PAPER);
                currentState = INTERNAL_WAITING_FOR_BUTTON;
            }
            threadSetToPaper = null;
        }

        /***********************************************************************
         * Utility functions
        ***********************************************************************/
        public static void typeDetected(short id)
        {
            objectType = id;
            currentState = INTERNAL_WAITING_FOR_BUTTON;
            playSound(SOUND_WAITING);
        }

        public static void changeMotorStatus(short motorId, short status)
        {
            if (TEST)
                return;
            if (status == MOTOR_STOP)
            {
                motors[motorId][MOTOR_LEFT].Write(false);
                motors[motorId][MOTOR_RIGHT].Write(false);
            }
            else if (status == MOTOR_LEFT)
            {
                motors[motorId][MOTOR_RIGHT].Write(false);
                motors[motorId][MOTOR_LEFT].Write(true);
            }
            else if (status == MOTOR_RIGHT)
            {
                motors[motorId][MOTOR_LEFT].Write(false);
                motors[motorId][MOTOR_RIGHT].Write(true);
            }
            else
            {
                // We should not be there.
                return;
            }
        }





        /***********************************************************************
         * Test cases
        ***********************************************************************/
        public static void testLog(string log)
        {
            if (TEST)
                testLogs += log + '|';
        }
        public static void test()
        {
            Debug.Print("Starting test cases");
            testPressAtTheWrongTimeFails();
            testButtons();
            testPaperHandling();
            testMetalHandling();
            testPlasticHandling();
            Debug.Print("All test cases run");
            Debug.Print(testErrorCount.ToString() + " failures.");
        }
        public static void testInitCase(string name)
        {
            testResetState();
            Debug.Print(" +======================================================");
            Debug.Print(" | " + name);
            Debug.Print(" +------------------------------------------------------");
        }
        public static void testCloseCase()
        {
            Debug.Print(" +======================================================");
            Debug.Print(" ");
        }
        public static void testResetState()
        {
            currentState = INTERNAL_SLEEPING;
            threadSetToPaper = null;
            topMotorThread = null;
            sortMotorThread = null;
            objectType = -1;
        }
        public static void testAssertLog(string expected)
        {
            if (testLogs != expected)
            {
                Debug.Print(" | Error: expected '" + expected + "' but got '" + testLogs + "'.");
                testErrorCount++;
            }
            else
                Debug.Print(" | Ok.");
            testLogs = "";
        }
        public static void testPressAtTheWrongTimeFails()
        {
            testInitCase("Error message if button pressed at the wrong time");
            DateTime datetime = new DateTime();
            onButtonMetal(0, 0, datetime);
            testAssertLog("Sound: not yet|");
            testResetState();
            onButtonPlastic(0, 0, datetime);
            testAssertLog("Sound: not yet|");
            testResetState();
            onButtonPaper(0, 0, datetime);
            testAssertLog("Sound: not yet|");
            testResetState();

            onSensorTop(0, 0, datetime);
            onButtonMetal(0, 0, datetime);
            testAssertLog("Motor: top, left, right, stop|Sound: not yet|");
            testResetState();
            onSensorTop(0, 0, datetime);
            onButtonPlastic(0, 0, datetime);
            testAssertLog("Motor: top, left, right, stop|Sound: not yet|");
            testResetState();
            onSensorTop(0, 0, datetime);
            onButtonPaper(0, 0, datetime);
            testAssertLog("Motor: top, left, right, stop|Sound: not yet|");
            testResetState();

            testCloseCase();
        }
        public static void testButtons()
        {
            testInitCase("Buttons");
            DateTime datetime = new DateTime();

            onSensorTop(0, 0, datetime);
            Thread.Sleep(3000);
            testLogs = "";
            onButtonPaper(0, 0, datetime);
            testAssertLog("Sound: ok|");
            testResetState();

            onSensorTop(0, 0, datetime);
            onSensorSort(0, 0, datetime);
            Thread.Sleep(3000);
            testLogs = "";
            onButtonPlastic(0, 0, datetime);
            testAssertLog("Sound: ok|");
            testResetState();

            onSensorTop(0, 0, datetime);
            onSensorSort(0, 0, datetime);
            Thread.Sleep(3000);
            testLogs = "";
            onButtonMetal(0, 0, datetime);
            testAssertLog("Sound: error|");
            testResetState();

            onSensorTop(0, 0, datetime);
            onSensorSort(0, 0, datetime);
            onSensorInductive(0, 0, datetime);
            testLogs = "";
            onButtonMetal(0, 0, datetime);
            testAssertLog("Sound: ok|");

            onSensorTop(0, 0, datetime);
            onSensorSort(0, 0, datetime);
            onSensorInductive(0, 0, datetime);
            testLogs = "";
            onButtonPlastic(0, 0, datetime);
            testAssertLog("Sound: error|");
            testResetState();

            testCloseCase();
        }
        public static void testPaperHandling()
        {
            testInitCase("Paper handling");
            DateTime datetime = new DateTime();
            onSensorTop(0, 0, datetime);
            testAssertLog("Motor: top, left, right, stop|");
            Thread.Sleep(3000);
            testAssertLog("Sound: waiting|");
            testCloseCase();
        }
        public static void testMetalHandling()
        {
            testInitCase("Metal handling");
            DateTime datetime = new DateTime();
            onSensorTop(0, 0, datetime);
            testAssertLog("Motor: top, left, right, stop|");
            onSensorInductive(0, 0, datetime);
            testAssertLog("Sound: waiting|Motor: sort, left, right, stop|");
            testCloseCase();
        }
        public static void testPlasticHandling()
        {
            testInitCase("Plastic handling");
            DateTime datetime = new DateTime();
            onSensorTop(0, 0, datetime);
            testAssertLog("Motor: top, left, right, stop|");
            onSensorSort(0, 0, datetime);
            Thread.Sleep(3000);
            testAssertLog("Sound: waiting|Motor: sort, right, left, stop|");
            testCloseCase();
        }
    }
}
