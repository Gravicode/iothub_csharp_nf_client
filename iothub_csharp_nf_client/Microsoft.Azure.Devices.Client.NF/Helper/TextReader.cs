using System;
using System.Collections;
using System.Text;


namespace System.IO
{
    [Serializable]
    public abstract class TextReader : MarshalByRefObject, IDisposable
    {
        public virtual void Close()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize((object)this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public virtual int Peek()
        {
            return -1;
        }

        public virtual int Read()
        {
            return -1;
        }

        public virtual int Read(char[] buffer, int index, int count)
        {
            return -1;
        }

        public virtual int ReadBlock(char[] buffer, int index, int count)
        {
            int num1 = 0;
            int num2;
            do
            {
                num1 += num2 = this.Read(buffer, index + num1, count - num1);
            }
            while (num2 > 0 && num1 < count);
            return num1;
        }

        public virtual string ReadToEnd()
        {
            return (string)null;
        }

        public virtual string ReadLine()
        {
            return (string)null;
        }
    }

}
