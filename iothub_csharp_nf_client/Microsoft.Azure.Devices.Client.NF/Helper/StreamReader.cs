using System;
using System.Collections;
using System.Text;

namespace System.IO
{
    public class StreamReader : TextReader
    {
        private const int c_MaxReadLineLen = 65535;
        private const int c_BufferSize = 512;
        private Stream m_stream;
        private Decoder m_decoder;
        private char[] m_singleCharBuff;
        private bool m_disposed;
        private byte[] m_buffer;
        private int m_curBufPos;
        private int m_curBufLen;

        public StreamReader(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException();
            if (!stream.CanRead)
                throw new ArgumentException();
            this.m_singleCharBuff = new char[1];
            this.m_buffer = new byte[512];
            this.m_curBufPos = 0;
            this.m_curBufLen = 0;
            this.m_stream = stream;
            this.m_decoder = this.CurrentEncoding.GetDecoder();
            this.m_disposed = false;
        }

        /*
        public StreamReader(string path)
          : this((Stream)new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
        }
        */
        public override void Close()
        {
            this.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (this.m_stream != null)
            {
                if (disposing)
                    this.m_stream.Close();
                this.m_stream = (Stream)null;
                this.m_buffer = (byte[])null;
                this.m_curBufPos = 0;
                this.m_curBufLen = 0;
            }
            this.m_disposed = true;
        }

        public override int Peek()
        {
            int num1 = this.m_curBufPos;
            if (this.m_curBufPos == this.m_curBufLen - 1 || ((int)this.m_buffer[this.m_curBufPos + 1] & 128) != 0 && this.m_curBufPos + 3 >= this.m_curBufLen)
            {
                int offset;
                for (offset = 0; offset < this.m_curBufLen - this.m_curBufPos - 1; ++offset)
                    this.m_buffer[offset] = this.m_buffer[this.m_curBufPos + offset];
                try
                {
                    while (this.m_stream.Length > 0L)
                    {
                        if (offset < this.m_buffer.Length)
                        {
                            int count = this.m_buffer.Length - offset;
                            if ((long)count > this.m_stream.Length)
                                count = (int)this.m_stream.Length;
                            int num2 = this.m_stream.Read(this.m_buffer, offset, count);
                            if (num2 > 0)
                                offset += num2;
                            else
                                break;
                        }
                        else
                            break;
                    }
                }
                catch (Exception ex)
                {
                    throw new IOException("m_stream.Read", ex);
                }
                num1 = 0;
                this.m_curBufPos = 0;
                this.m_curBufLen = offset;
            }
            int num3 = this.Read();
            this.m_curBufPos = num1;
            return num3;
        }

        public override int Read()
        {
            bool completed = false;
            int count;
            do
            {
                int bytesUsed;
                int charsUsed;
                this.m_decoder.Convert(this.m_buffer, this.m_curBufPos, this.m_curBufLen - this.m_curBufPos, this.m_singleCharBuff, 0, 1, false, out bytesUsed, out charsUsed, out completed);
                this.m_curBufPos += bytesUsed;
                if (charsUsed != 1)
                {
                    int length = this.m_buffer.Length;
                    count = length > (int)this.m_stream.Length ? (int)this.m_stream.Length : length;
                    if (count == 0)
                        count = 1;
                }
                else
                    goto label_6;
            }
            while (this.FillBufferAndReset(count) != 0);
            return -1;
        label_6:
            return (int)this.m_singleCharBuff[0];
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            if (index < 0)
                throw new ArgumentOutOfRangeException();
            if (count < 0)
                throw new ArgumentOutOfRangeException();
            if (buffer.Length - index < count)
                throw new ArgumentException();
            if (this.m_disposed)
                throw new ObjectDisposedException();
            int charsUsed = 0;
            bool completed = false;
            if (this.m_curBufLen == 0)
                this.FillBufferAndReset(count);
            int charIndex = 0;
            do
            {
                int bytesUsed;
                this.m_decoder.Convert(this.m_buffer, this.m_curBufPos, this.m_curBufLen - this.m_curBufPos, buffer, charIndex, count, false, out bytesUsed, out charsUsed, out completed);
                count -= charsUsed;
                this.m_curBufPos += bytesUsed;
                charIndex += charsUsed;
            }
            while (count != 0 && this.FillBufferAndReset(count) != 0);
            return charsUsed;
        }

        public override string ReadLine()
        {
            int length1 = 512;
            char[] chArray1 = new char[length1];
            int num1 = 512;
            int length2 = 0;
            int num2;
            while ((num2 = this.Read()) != -1)
            {
                if (length2 == length1)
                {
                    if (length1 + num1 > (int)ushort.MaxValue)
                        throw new Exception();
                    char[] chArray2 = new char[length1 + num1];
                    Array.Copy((Array)chArray1, 0, (Array)chArray2, 0, length1);
                    chArray1 = chArray2;
                    length1 += num1;
                }
                chArray1[length2] = (char)num2;
                if (chArray1[length2] == '\n')
                    return new string(chArray1, 0, length2);
                if (chArray1[length2] == '\r')
                {
                    if (this.Peek() == 10)
                        this.Read();
                    return new string(chArray1, 0, length2);
                }
                ++length2;
            }
            if (length2 == 0)
                return (string)null;
            return new string(chArray1, 0, length2);
        }

        public override string ReadToEnd()
        {
            return new string(!this.m_stream.CanSeek ? this.ReadNonSeekableStream() : this.ReadSeekableStream());
        }

        private char[] ReadSeekableStream()
        {
            char[] buffer = new char[(int)this.m_stream.Length];
            this.Read(buffer, 0, buffer.Length);
            return buffer;
        }

        private char[] ReadNonSeekableStream()
        {
            ArrayList arrayList = new ArrayList();
            int length1 = 0;
            char[] chArray1 = (char[])null;
            bool flag = false;
            do
            {
                char[] buffer = new char[512];
                int length2 = this.Read(buffer, 0, buffer.Length);
                length1 += length2;
                if (length2 < 512)
                {
                    if (length2 > 0)
                    {
                        char[] chArray2 = new char[length2];
                        Array.Copy((Array)buffer, (Array)chArray2, length2);
                        chArray1 = chArray2;
                    }
                    flag = true;
                }
                else
                    chArray1 = buffer;
                arrayList.Add((object)chArray1);
            }
            while (!flag);
            if (arrayList.Count <= 1)
                return (char[])arrayList[0];
            char[] chArray3 = new char[length1];
            int index1 = 0;
            for (int index2 = 0; index2 < arrayList.Count; ++index2)
            {
                char[] chArray2 = (char[])arrayList[index2];
                chArray2.CopyTo((Array)chArray3, index1);
                index1 += chArray2.Length;
            }
            return chArray3;
        }

        public virtual Stream BaseStream
        {
            get
            {
                return this.m_stream;
            }
        }

        public virtual Encoding CurrentEncoding
        {
            get
            {
                return Encoding.UTF8;
            }
        }

        public bool EndOfStream
        {
            get
            {
                return this.m_curBufLen == this.m_curBufPos;
            }
        }

        private int FillBufferAndReset(int count)
        {
            if (this.m_curBufPos != 0)
                this.Reset();
            int num1 = 0;
            try
            {
                int num2;
                for (; count > 0; count -= num2)
                {
                    if (this.m_curBufLen < this.m_buffer.Length)
                    {
                        int num3 = this.m_buffer.Length - this.m_curBufLen;
                        if (count > num3)
                            count = num3;
                        num2 = this.m_stream.Read(this.m_buffer, this.m_curBufLen, count);
                        if (num2 != 0)
                        {
                            num1 += num2;
                            this.m_curBufLen += num2;
                        }
                        else
                            break;
                    }
                    else
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new IOException("m_stream.Read", ex);
            }
            return num1;
        }

        private void Reset()
        {
            int length = this.m_curBufLen - this.m_curBufPos;
            Array.Copy((Array)this.m_buffer, this.m_curBufPos, (Array)this.m_buffer, 0, length);
            this.m_curBufPos = 0;
            this.m_curBufLen = length;
        }
    }
}
