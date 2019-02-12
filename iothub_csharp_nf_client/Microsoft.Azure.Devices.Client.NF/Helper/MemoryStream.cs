using System;
using System.Collections;
using System.Text;

namespace System.IO
{
    public class MemoryStream : Stream
    {
        private const int MemStreamMaxLength = 65535;
        private byte[] _buffer;
        private int _origin;
        private int _position;
        private int _length;
        private int _capacity;
        private bool _expandable;
        private bool _isOpen;

        public MemoryStream()
        {
            this._buffer = new byte[256];
            this._capacity = 256;
            this._expandable = true;
            this._origin = 0;
            this._isOpen = true;
        }

        public MemoryStream(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException();
            this._buffer = buffer;
            this._length = this._capacity = buffer.Length;
            this._expandable = false;
            this._origin = 0;
            this._isOpen = true;
        }

        public override bool CanRead
        {
            get
            {
                return this._isOpen;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this._isOpen;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this._isOpen;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
                return;
            this._isOpen = false;
        }

        private bool EnsureCapacity(int value)
        {
            if (value <= this._capacity)
                return false;
            int length = value;
            if (length < 256)
                length = 256;
            if (length < this._capacity * 2)
                length = this._capacity * 2;
            if (!this._expandable && length > this._capacity)
                throw new NotSupportedException();
            if (length > 0)
            {
                byte[] numArray = new byte[length];
                if (this._length > 0)
                    Array.Copy((Array)this._buffer, 0, (Array)numArray, 0, this._length);
                this._buffer = numArray;
            }
            else
                this._buffer = (byte[])null;
            this._capacity = length;
            return true;
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get
            {
                if (!this._isOpen)
                    throw new ObjectDisposedException();
                return (long)(this._length - this._origin);
            }
        }

        public override long Position
        {
            get
            {
                if (!this._isOpen)
                    throw new ObjectDisposedException();
                return (long)(this._position - this._origin);
            }
            set
            {
                if (!this._isOpen)
                    throw new ObjectDisposedException();
                if (value < 0L || value > (long)ushort.MaxValue)
                    throw new ArgumentOutOfRangeException();
                this._position = this._origin + (int)value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (buffer == null)
                throw new ArgumentNullException();
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();
            if (buffer.Length - offset < count)
                throw new ArgumentException();
            int length = this._length - this._position;
            if (length > count)
                length = count;
            if (length <= 0)
                return 0;
            Array.Copy((Array)this._buffer, this._position, (Array)buffer, offset, length);
            this._position += length;
            return length;
        }

        public override int ReadByte()
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (this._position >= this._length)
                return -1;
            return (int)this._buffer[this._position++];
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (offset > (long)ushort.MaxValue)
                throw new ArgumentOutOfRangeException();
            switch (origin)
            {
                case SeekOrigin.Begin:
                    if (offset < 0L)
                        throw new IOException();
                    this._position = this._origin + (int)offset;
                    break;
                case SeekOrigin.Current:
                    if (offset + (long)this._position < (long)this._origin)
                        throw new IOException();
                    this._position += (int)offset;
                    break;
                case SeekOrigin.End:
                    if ((long)this._length + offset < (long)this._origin)
                        throw new IOException();
                    this._position = this._length + (int)offset;
                    break;
                default:
                    throw new ArgumentException();
            }
            return (long)this._position;
        }

        public override void SetLength(long value)
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (value > (long)ushort.MaxValue || value < 0L)
                throw new ArgumentOutOfRangeException();
            int num = this._origin + (int)value;
            if (!this.EnsureCapacity(num) && num > this._length)
                Array.Clear((Array)this._buffer, this._length, num - this._length);
            this._length = num;
            if (this._position <= num)
                return;
            this._position = num;
        }

        public virtual byte[] ToArray()
        {
            byte[] numArray = new byte[this._length - this._origin];
            Array.Copy((Array)this._buffer, this._origin, (Array)numArray, 0, this._length - this._origin);
            return numArray;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (buffer == null)
                throw new ArgumentNullException();
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();
            if (buffer.Length - offset < count)
                throw new ArgumentException();
            int num = this._position + count;
            if (num > this._length)
            {
                if (num > this._capacity)
                    this.EnsureCapacity(num);
                this._length = num;
            }
            Array.Copy((Array)buffer, offset, (Array)this._buffer, this._position, count);
            this._position = num;
        }

        public override void WriteByte(byte value)
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (this._position >= this._capacity)
                this.EnsureCapacity(this._position + 1);
            this._buffer[this._position++] = value;
            if (this._position <= this._length)
                return;
            this._length = this._position;
        }

        public virtual void WriteTo(Stream stream)
        {
            if (!this._isOpen)
                throw new ObjectDisposedException();
            if (stream == null)
                throw new ArgumentNullException();
            stream.Write(this._buffer, this._origin, this._length - this._origin);
        }
    }
}
