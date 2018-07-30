using System;
using System.IO;

namespace apak
{
    /// <summary>
    /// Provides a Stream that only exposes a certain Range from another stream
    /// </summary>
    /// <remarks>This is always readonly</remarks>
    internal class RangedStream : Stream
    {
        /// <summary>
        /// Gets if this instance can Read
        /// </summary>
        public override bool CanRead
        {
            get
            {
                return IN.CanRead;
            }
        }

        /// <summary>
        /// Gets if this instance can Seek
        /// </summary>
        public override bool CanSeek
        {
            get
            {
                return IN.CanSeek;
            }
        }

        /// <summary>
        /// Gets 'false'
        /// </summary>
        /// <remarks>See <see cref="Write(byte[], int, int)"/> for instructions on how to enable Write support</remarks>
        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets the Length of this instance
        /// </summary>
        public override long Length
        {
            get
            {
                return Len;
            }
        }

        /// <summary>
        /// Gets or sets the position of this instance
        /// </summary>
        /// <remarks>This is processed directly on the Base Stream</remarks>
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

        /// <summary>
        /// Gets the Base Stream for this Instance
        /// </summary>
        public Stream BaseStream
        {
            get
            {
                return IN;
            }
        }

        /// <summary>
        /// Offset from the Base stresm Start
        /// </summary>
        /// <remarks>
        /// We don't store the relative position itself.
        /// This allows other components to operate on the base stream too
        /// </remarks>
        private long Offset = 0;
        /// <summary>
        /// Virtual Length of the stream
        /// </summary>
        private long Len;
        /// <summary>
        /// Base Stream
        /// </summary>
        private Stream IN;

        /// <summary>
        /// Creates a new Stream Range
        /// </summary>
        /// <param name="BaseStream">Base Stream</param>
        /// <param name="Length">Length to Expose</param>
        public RangedStream(Stream BaseStream, long Length)
        {
            Len = Length;
            IN = BaseStream;
            Offset = BaseStream.Position;
            if (Len > IN.Position + IN.Length)
            {
                throw new ArgumentOutOfRangeException("Length", "RangedStream can't be longer than the base stream passed to it");
            }
        }

        #region Bypasses

        /// <summary>
        /// This does nothing.
        /// </summary>
        /// <remarks>The Base Stream is never closed or disposed</remarks>
        public override void Close()
        {
            //Do nothing
        }

        /// <summary>
        /// This does nothing.
        /// </summary>
        /// <remarks>The Base Stream is never closed or disposed</remarks>
        public new void Dispose()
        {
            //Do nothing
        }

        /// <summary>
        /// This does nothing.
        /// </summary>
        /// <remarks>The Base Stream is never closed or disposed</remarks>
        protected override void Dispose(bool disposing)
        {
            //Do nothing
        }

        #endregion

        /// <summary>
        /// Flushes the Base Stream
        /// </summary>
        public override void Flush()
        {
            IN.Flush();
        }

        /// <summary>
        /// Reads from the Base Stream
        /// </summary>
        /// <param name="buffer">Byte Buffer</param>
        /// <param name="offset">Offset in Buffer</param>
        /// <param name="count">Number of Bytes maximum</param>
        /// <returns>Number of Bytes actually read</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            //Disallow to read from the Base if it's no longer in Range
            if (Position < 0 || Position > Len)
            {
                throw new InvalidOperationException($"The Base Stream is currently outside of the bounds of this instance. Position={Position} Length={Len}");
            }
            //Get the Real count we want to use, in case count is larger than allowed anymore
            var RealCount = (int)Math.Min(Len - Position, count);
            return IN.Read(buffer, offset, RealCount);
        }

        /// <summary>
        /// Seek the Base stream
        /// </summary>
        /// <param name="offset">Offset to seek to</param>
        /// <param name="origin">Base to calculate offset from</param>
        /// <returns>new Position</returns>
        /// <remarks>
        /// It's not possible to seek the base stream outside of the bounds specified in the Contructor.
        /// Seek the base Stream directly if needed.
        /// Use <see cref="SetLength(long)"/> to extend or shrink the Range
        /// </remarks>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (!CanSeek)
            {
                throw new NotSupportedException($"Stream is not seekable");
            }
            //Calculate absolute Position in regards to this instance, not the base stream
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
            //Check if we are in the allowed range
            if (NewPos < 0)
            {
                throw new IOException($"Unable to seek before start of Stream. Offset={NewPos}");
            }
            if (NewPos > Len)
            {
                throw new IOException($"Unable to seek beyond end of Stream. Length={Len} Offset={NewPos}");
            }
            //Seek base stream relative to our offset and return Relative Offset of this instance
            return Offset - IN.Seek(Offset + NewPos, SeekOrigin.Begin);
        }

        /// <summary>
        /// Extends or shrinks the Range of this Stream
        /// </summary>
        /// <param name="value">New Length</param>
        /// <remarks>
        /// This will not do anything to the Base Stream and using numbers outside of Bounds will throw an Exception
        /// </remarks>
        public override void SetLength(long value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("value", "Length must be at least 0");
            }
            if (value < Position)
            {
                throw new ArgumentOutOfRangeException("value", "Unable to truncate before the current Position");
            }
            if (value + Offset > BaseStream.Length)
            {
                throw new ArgumentOutOfRangeException("value", "Unable to extend beyond the Base Stream Length. Use SetLength on the BaseStream property instead to extend the Base Stream");
            }
            Len = value;
        }

        /// <summary>
        /// Tests if Exceptions work
        /// </summary>
        /// <param name="buffer">Ignored</param>
        /// <param name="offset">Ignored</param>
        /// <param name="count">Ignored</param>
        /// <remarks>This always throws</remarks>
        /// <exception cref="NotSupportedException">Always thrown</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            //Implementing this is not actually that hard but we don't need it here so it's missing:
            //1. Lock Base Stream
            //2. Write to the Base
            //3. extend the "Len" Field of this Instance if the new Position is beyond our virtual End.
            //4. Release Lock
            throw new NotSupportedException("RangedStream does not supports writing. Write directly to the Base Stream");
        }
    }
}
