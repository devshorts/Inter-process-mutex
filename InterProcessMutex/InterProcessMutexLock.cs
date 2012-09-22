using System;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

namespace InterProcessMutex
{
    /// <summary>
    /// Provides a wrapped way to create critical sections across multiple processes using named mutexes
    /// </summary>
    public class InterProcessMutexLock : IDisposable
    {
        private readonly String _mutexName;

        private readonly Mutex _currentMutex;

        private bool _created;
        public bool Created
        {
            get { return _created; }
            set { _created = value; }
        }

        public InterProcessMutexLock(String mutexName)
        {
            try
            {
                _mutexName = mutexName;

                try
                {
                    _currentMutex = Mutex.OpenExisting(_mutexName);
                }
                catch (WaitHandleCannotBeOpenedException)
                {
                    // grant everyone access to the mutex
                    var security = new MutexSecurity();
                    var everyoneIdentity = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                    var rule = new MutexAccessRule(everyoneIdentity, MutexRights.FullControl, AccessControlType.Allow);
                    security.AddAccessRule(rule);

                    // make sure to not initially own it, because if you do it also acquires the lock
                    // we want to explicitly attempt to acquire the lock ourselves so we know how many times
                    // this object acquired and released the lock
                    _currentMutex = new Mutex(false, mutexName, out _created, security);
                }

                AquireMutex();
            }
            catch(Exception ex)
            {
                var exceptionString = String.Format("Exception in InterProcessMutexLock, mutex name {0}", mutexName);
                Log.Error(this, exceptionString, ex);
                throw ExceptionUtil.Rethrow(ex, exceptionString);
            }
        }

        private void AquireMutex()
        {
            try
            {
                _currentMutex.WaitOne();
            }
            catch (AbandonedMutexException ex)
            {
                try
                {
                    Log.Error(this, "An abandoned mutex was encountered, attempting to release", ex);
                    _currentMutex.ReleaseMutex();

                    Log.Debug(this, "Abandonded mutex was released and now aquiring");

                    _currentMutex.WaitOne();                    
                }
                catch(Exception abandondedMutexEx)
                {
                    throw ExceptionUtil.Rethrow(abandondedMutexEx, "tried to re-acquire abandoned mutex but failed");
                }
            }
            catch(Exception ex)
            {
                var exceptionString = "An unexpected error occurred acquiring mutex " + _mutexName;
                throw ExceptionUtil.Rethrow(ex, exceptionString);
            }
        }


        protected void Dispose(bool disposing)
        {
            if(disposing)
            {
                if (_currentMutex != null)
                {
                    _currentMutex.ReleaseMutex();
                    _currentMutex.Dispose();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }

    public static class Log
    {
        public static void Error(object source, String format, Exception ex)
        {
            Console.WriteLine(String.Format("{0}: {1} - {2}", source, format, ex));
        }

        public static void Debug(object source, String format)
        {
            Error(source, format, null);
        }
    }

    public static class ExceptionUtil
    {
        public static Exception Rethrow(Exception ex, String msg)
        {
            return ex;
        }
    }
}
