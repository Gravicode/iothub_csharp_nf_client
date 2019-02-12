using System;
using System.Collections;
using System.Text;

namespace Microsoft.Azure.Devices.Client
{
    public static class MyDateTimeExtension
    {
        /*
        private static Int64 GetTime()
        {
            Int64 retval = 0;
            var st = new DateTime(1970, 1, 1);
            TimeSpan t = (DateTime.UtcNow - st);
            retval = (Int64)(t.TotalMilliseconds + 0.5);
            return retval;
        }*/
        public static DateTime ToUniversalTime(this DateTime time,TimeSpan OffSet)
        {
            
            if (time.Kind == DateTimeKind.Utc)
                return time;
            return new DateTime(time.Ticks + OffSet.Ticks, DateTimeKind.Utc);
        }
    
    }
}
