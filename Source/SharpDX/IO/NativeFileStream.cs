// Copyright (c) 2010-2011 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SharpDX.IO
{
    /// <summary>
    /// Windows File Helper.
    /// </summary>
    public class NativeFileStream : Stream
    {
        private bool canRead;
        private bool canWrite;
        private bool canSeek;
        private IntPtr handle;
        private long position;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeFileStream"/> class.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="fileMode">The file mode.</param>
        /// <param name="access">The access mode.</param>
        /// <param name="share">The share mode.</param>
        public NativeFileStream(string fileName, NativeFileMode fileMode, NativeFileAccess access, NativeFileShare share = NativeFileShare.Read)
        {
#if WIN8
            handle = NativeFile.Create(fileName, access, share, fileMode, IntPtr.Zero);
#else
            handle = NativeFile.Create(fileName, access, share, IntPtr.Zero, fileMode, NativeFileOptions.None, IntPtr.Zero);
#endif
            if (handle.ToInt32() == -1)
                throw new IOException(string.Format("Unable to open file {0}", fileName), Marshal.GetLastWin32Error());

            canRead = true;
            canWrite = true;
            canSeek = true;
        }

        /// <inheritdoc/>
        public override void Flush()
        {
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            long newPosition = 0;
            int result = NativeFile.SetFilePointerEx(handle, offset, out newPosition, origin);
            if (result != 0)
                throw new IOException("Unable to seek to this position", Marshal.GetLastWin32Error());

            return newPosition;
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            long newPosition;
            int result = NativeFile.SetFilePointerEx(handle, value, out newPosition, SeekOrigin.Begin);
            if (result != 0)
                throw new IOException("Unable to seek to this position", Marshal.GetLastWin32Error());
            NativeFile.SetEndOfFile(handle);
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int numberOfBytesRead;
            unsafe
            {
                fixed (void* pbuffer = &buffer[offset])
                {
                    var result = NativeFile.ReadFile(handle, (IntPtr)pbuffer, count, out numberOfBytesRead, IntPtr.Zero);
                    result.CheckError();
                }
                position += numberOfBytesRead;
            }
            return numberOfBytesRead;
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            int numberOfBytesWritten;
            unsafe
            {
                fixed (void* pbuffer = &buffer[offset])
                {
                    var result = NativeFile.WriteFile(handle, (IntPtr)pbuffer, count, out numberOfBytesWritten, IntPtr.Zero);
                    result.CheckError();
                }
                position += numberOfBytesWritten;
            }
        }

        /// <inheritdoc/>
        public override bool CanRead
        {
            get
            {
                return canRead;
            }
        }

        /// <inheritdoc/>
        public override bool CanSeek
        {
            get
            {
                return canSeek;
            }
        }

        /// <inheritdoc/>
        public override bool CanWrite
        {
            get
            {
                return canWrite;
            }
        }

        /// <inheritdoc/>
        public override long Length
        {
            get
            {
                long length;
                if ( NativeFile.GetFileSizeEx(handle, out length) != 0 )
                    throw new IOException("Unable to get length", Marshal.GetLastWin32Error());
                return length;
            }
        }

        /// <inheritdoc/>
        public override long Position
        {
            get
            {
                return position;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
                position = value;
            }
        }
    }
}