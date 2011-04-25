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
        static Thread timeoutThread;
        static Thread topMotorThread;
        static Thread sortMotorThread;

        /***********************************************************************
         * Constants
        ***********************************************************************/
        // Constants used for arrays
        const short PLASTIC = 0;
        const short METAL = 1;
        const short PAPER = 2;
        const short NUMBER_OF_BUTTONS = 3;

        const short SENSOR_PAPER_BASKET = 0;
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

        // Ports
        static InterruptPort[] buttons = new InterruptPort[NUMBER_OF_BUTTONS];
        static InterruptPort[] sensors = new InterruptPort[NUMBER_OF_SENSORS];
        static OutputPort[][] motors = new OutputPort[NUMBER_OF_MOTORS][];

        // Sounds
        static short SOUND_NONE = 0;
        static short SOUND_WAITING = 1;
        static short SOUND_NOT_YET = 2; // Shouldn't have pressed a button yet
        static short SOUND_OK = 3;
        static short SOUND_ERROR = 4;

        // Internal state stuff
        const short INTERNAL_SLEEPING = 0;
        const short INTERNAL_FALLING = 1;
        const short INTERNAL_WAITING_FOR_BUTTON = 3;
        static short currentState = INTERNAL_SLEEPING;
        static short objectType;

        // Timeouts (in milliseconds)
        static int TIMEOUT = 10000;
        static int TIME_OPEN_MOTOR_TOP = 3000;
        static int TIME_CLOSE_MOTOR_TOP = 3000;
        static int TIME_PLASTIC_DOWN = 3000;
        static int TIME_PLASTIC_UP = 3000;
        static int TIME_METAL_DOWN = 3000;
        static int TIME_METAL_UP = 3000;
        static int TIMEOUT_DETECT_AS_PLASTIC = 2000; // If the top board opened, and this timer ends without any other event, we consider the object type is PLASTIC

        /***********************************************************************
         * Initialization
        ***********************************************************************/
        public static void Main()
        {
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
            buttons[PLASTIC] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di4, true,Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            buttons[METAL] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di5, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            buttons[PAPER] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di6, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);

            // Initialize the button interruptions
            buttons[PLASTIC].OnInterrupt += new NativeEventHandler(onButtonPlastic);
            buttons[METAL].OnInterrupt += new NativeEventHandler(onButtonMetal);
            buttons[PAPER].OnInterrupt += new NativeEventHandler(onButtonPaper);


            // Initialize the sensors
            sensors[SENSOR_PAPER_BASKET] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di0, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[SENSOR_TOP] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di1, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);
            sensors[SENSOR_INDUCTIVE] = new InterruptPort((Cpu.Pin)FEZ_Pin.Interrupt.Di12, true, Port.ResistorMode.PullUp, Port.InterruptMode.InterruptEdgeHigh);

            // Initialize the sensors interruptions:
            sensors[SENSOR_PAPER_BASKET].OnInterrupt += new NativeEventHandler(onSensorPaperBasket);
            sensors[SENSOR_TOP].OnInterrupt += new NativeEventHandler(onSensorTop);
            sensors[SENSOR_INDUCTIVE].OnInterrupt += new NativeEventHandler(onSensorInductive);

            // Initialize the motors
            motors[MOTOR_TOP] = new OutputPort[NUMBER_OF_PORTS_PER_MOTOR];
            motors[MOTOR_SORT] = new OutputPort[NUMBER_OF_PORTS_PER_MOTOR];
            motors[MOTOR_TOP][MOTOR_LEFT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di8, false);
            motors[MOTOR_TOP][MOTOR_RIGHT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di9, false);
            motors[MOTOR_SORT][MOTOR_LEFT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di11, false);
            motors[MOTOR_SORT][MOTOR_RIGHT] = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.Di10, false);
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
                    playSound(SOUND_OK);
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
            // TODO: really implement this

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
        /***********************************************************************
         * Sensors handling
        ***********************************************************************/
        public static void onSensorTop(uint port, uint state, DateTime time)
        {
            currentState = INTERNAL_FALLING;
            setToPlasticIfDetectionTimesOut();
            if (TEST)
                testLog("Motor: top, left, right, stop");
            else
            {
                Thread thread = new Thread(_onSensorTop);
                try
                {
                    topMotorThread.Abort();
                }
                catch (NullReferenceException) { } // No thread currently running
                catch (ThreadAbortException) { }
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
        public static void onSensorPaperBasket(uint port, uint state, DateTime time)
        {
            typeDetected(PAPER);
        }
        public static void onSensorInductive(uint port, uint state, DateTime time)
        {
            typeDetected(METAL);
            if (TEST)
                testLog("Motor: sort, left, right, stop");
            else
            {
                Thread thread = new Thread(_onSensorInductive);
                try
                {
                    topMotorThread.Abort();
                }
                catch (NullReferenceException) { } // No thread currently running
                catch (ThreadAbortException) { }
            }
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
            if (TEST)
                testLog("Thread: _setToPlasticIfDetectionTimesOut");
            else
            {
                Thread thread = new Thread(_setToPlasticIfDetectionTimesOut);
                try
                {
                    timeoutThread.Abort();
                }
                catch (NullReferenceException) { } // No thread currently running
                catch (ThreadAbortException) { } // This shouldn't happen, but we use it, just in case
                timeoutThread = thread;
                thread.Start();
            }
        }
        public static void _setToPlasticIfDetectionTimesOut()
        {
            if (TEST)
            {
                typeDetected(PLASTIC);
                testLog("Motor: sort, right, left, stop");
            }
            else
            {
                Thread.Sleep(TIMEOUT_DETECT_AS_PLASTIC);
                typeDetected(PLASTIC);
                changeMotorStatus(MOTOR_SORT, MOTOR_RIGHT);
                Thread.Sleep(TIME_PLASTIC_DOWN);
                changeMotorStatus(MOTOR_SORT, MOTOR_LEFT);
                Thread.Sleep(TIME_PLASTIC_UP);
                changeMotorStatus(MOTOR_SORT, MOTOR_STOP);
                timeoutThread = null;
            }
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
                motors[motorId][MOTOR_RIGHT].Write(true);
                motors[motorId][MOTOR_LEFT].Write(false);
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
            timeoutThread = null;
            topMotorThread = null;
            sortMotorThread = null;

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
            testAssertLog("Thread: _setToPlasticIfDetectionTimesOut|Motor: top, left, right, stop|Sound: not yet|");
            testResetState();
            onSensorTop(0, 0, datetime);
            onButtonPlastic(0, 0, datetime);
            testAssertLog("Thread: _setToPlasticIfDetectionTimesOut|Motor: top, left, right, stop|Sound: not yet|");
            testResetState();
            onSensorTop(0, 0, datetime);
            onButtonPaper(0, 0, datetime);
            testAssertLog("Thread: _setToPlasticIfDetectionTimesOut|Motor: top, left, right, stop|Sound: not yet|");
            testResetState();

            testCloseCase();
        }
        public static void testButtons()
        {
            testInitCase("Buttons");
            DateTime datetime = new DateTime();

            onSensorTop(0, 0, datetime);
            onSensorPaperBasket(0, 0, datetime);
            testLogs = "";
            onButtonPaper(0, 0, datetime);
            testAssertLog("Sound: ok|");
            testResetState();

            onSensorTop(0, 0, datetime);
            onSensorPaperBasket(0, 0, datetime);
            testLogs = "";
            onButtonMetal(0, 0, datetime);
            testAssertLog("Sound: error|");
            testResetState();

            testCloseCase();
        }
        public static void testPaperHandling()
        {
            testInitCase("Paper handling");
            DateTime datetime = new DateTime();
            onSensorTop(0, 0, datetime);
            testAssertLog("Thread: _setToPlasticIfDetectionTimesOut|Motor: top, left, right, stop|");
            onSensorPaperBasket(0, 0, datetime);
            testAssertLog("Sound: waiting|");
            testCloseCase();
        }
        public static void testMetalHandling()
        {
            testInitCase("Metal handling");
            DateTime datetime = new DateTime();
            onSensorTop(0, 0, datetime);
            testAssertLog("Thread: _setToPlasticIfDetectionTimesOut|Motor: top, left, right, stop|");
            onSensorInductive(0, 0, datetime);
            testAssertLog("Sound: waiting|Motor: sort, left, right, stop|");
            testCloseCase();
        }
        public static void testPlasticHandling()
        {
            testInitCase("Plastic handling");
            DateTime datetime = new DateTime();
            onSensorTop(0, 0, datetime);
            testAssertLog("Thread: _setToPlasticIfDetectionTimesOut|Motor: top, left, right, stop|");
            _setToPlasticIfDetectionTimesOut();
            testAssertLog("Sound: waiting|Motor: sort, right, left, stop|");
            testCloseCase();
        }
    }
}
