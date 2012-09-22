using System;
using System.Threading;
using InterProcessMutex;
using NUnit.Framework;

namespace InterProcessMutexTests
{
    [TestFixture]
    public class InterProcessLockTests
    {
        [Test]
        public void AbandondedMutexTest()
        {
            var synchronizedLockValue = 1;

            const string mutexName = "testMutex";

            var thread1 = ThreadUtil.Start("ownMutex1", () =>
            {
                // don't close this mutex though
                var mutex = new Mutex(true, mutexName);                                                             
            });

            thread1.Join();

            var thread2 = ThreadUtil.Start("ownMutex2", () =>
            {
                // we should get an anbonded mutex here but then handle it gracefully
                using(new InterProcessMutexLock(mutexName))
                {
                    // hold onto the lock for 2 seconds
                    Thread.Sleep(TimeSpan.FromSeconds(2));

                    // if this isn't in a critical secton, ownMutex3 thread will overwrite it
                    synchronizedLockValue = 2;
                }
            });

            // give thread2 enough time to aquire the abandonded lock and handle it
            Thread.Sleep(TimeSpan.FromSeconds(1));

            // now synch another thread to the newly released lock
            var thread3 = ThreadUtil.Start("ownMutex3", () =>
            {
                // we should get an anbonded mutex here but then handle it gracefully
                using (new InterProcessMutexLock(mutexName))
                {
                    synchronizedLockValue = 3;
                }
            });

            thread3.Join();
            thread2.Join();

            // make sure that ownMutex3 waited on ownMutex2 and set the final value to 3
            Assert.True(synchronizedLockValue == 3, "Exception occurred acquiring the lock");
        }

        [Test]
        public void BlockWait()
        {
            var synchronizedLockValue = 1;

            const string mutexName = "testMutex2";

            var thread1 = ThreadUtil.Start("ownMutex1", () =>
            {
                using (new InterProcessMutexLock(mutexName))
                {
                    synchronizedLockValue = 2;
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            });

            Thread.Sleep(TimeSpan.FromSeconds(2));

            var thread2 = ThreadUtil.Start("ownMutex2", () =>
            {
                using (new InterProcessMutexLock(mutexName))
                {
                    synchronizedLockValue = 10;
                }
            });

            thread1.Join();
            thread2.Join();

            Assert.True(synchronizedLockValue == 10, "InterProcessLock didn't block second request");
        }
    }

    public static  class ThreadUtil
    {
        public static Thread Start(string name, ThreadStart action)
        {
            var thread = new Thread(action)
                             {
                                 Name = name
                             };
            thread.Start();
            return thread;
        }
    }
}
