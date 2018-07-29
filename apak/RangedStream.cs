using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace apak
{
    internal class RangedStream : Stream
    {
        public override bool CanRead
        {
            get
            {
                return IN.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return IN.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                return Len;
            }
        }

        public override long Position
        {
            get
            {
                return Math.Max(0L, IN.Position - Offset);
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        private long Offset = 0;
        private long Len;
        private Stream IN;

        public RangedStream(Stream BaseStream, long Length)
        {
            Len = Length;
            IN = BaseStream;
            Offset = BaseStream.Position;
        }

        public new void Dispose()
        {
            //Do nothing
        }

        protected override void Dispose(bool disposing)
        {
            //Do nothing
        }

        public override void Flush()
        {
            IN.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var RealCount = (int)Math.Min(Len - Position, count);
            var Readed = IN.Read(buffer, offset, RealCount);
            return Readed;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!CanSeek)
            {
                throw new NotSupportedException($"Stream is not seekable");
            }
            long NewPos = 0;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    NewPos = offset;
                    break;
                case SeekOrigin.Current:
                    NewPos = Position + offset;
                    break;
                case SeekOrigin.End:
                    NewPos = Len + offset;
                    break;
            }
            if (NewPos < 0)
            {
                throw new IOException($"Unable to seek before start of Stream. Offset={NewPos}");
            }
            if (NewPos > Len)
            {
                throw new IOException($"Unable to seek beyond end of Stream. Length={Len} Offset={NewPos}");
            }
            //Seek base stream relative to our offset
            return Offset - IN.Seek(Offset + NewPos, SeekOrigin.Begin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException("This stream is Readonly");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("This stream is Readonly");
        }
    }
}
