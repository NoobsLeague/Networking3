using System;
using System.Net.Sockets;
using System.IO;

namespace shared
{
    /**
     * Enhanced StreamUtil class with proper error handling for TCP communication.
     * Handles disconnection scenarios gracefully and provides timeout support.
     */
    public static class StreamUtil
    {
        private const int DEFAULT_TIMEOUT_MS = 5000; // 5 second timeout

        /**
         * Writes the size of the given byte array into the stream and then the bytes themselves.
         * Returns true if successful, false if the operation failed.
         */
        public static bool Write(NetworkStream pStream, byte[] pMessage)
        {
            return Write(pStream, pMessage, DEFAULT_TIMEOUT_MS);
        }

        /**
         * Writes with custom timeout. Returns true if successful, false if failed.
         */
        public static bool Write(NetworkStream pStream, byte[] pMessage, int timeoutMs)
        {
            try
            {
                // Check if stream is still valid
                if (pStream == null || !pStream.CanWrite)
                    return false;

                // Set write timeout
                pStream.WriteTimeout = timeoutMs;

                // Convert message length to 4 bytes and write those bytes into the stream
                byte[] lengthBytes = BitConverter.GetBytes(pMessage.Length);
                pStream.Write(lengthBytes, 0, 4);

                // Now send the bytes of the message themselves
                pStream.Write(pMessage, 0, pMessage.Length);

                // Ensure data is sent immediately
                pStream.Flush();

                return true;
            }
            catch (Exception ex)
            {
                // Log the specific error type for debugging
                Console.WriteLine($"StreamUtil.Write failed: {ex.GetType().Name} - {ex.Message}");
                return false;
            }
        }

        /**
         * Reads the amount of bytes to receive from the stream and then the bytes themselves.
         * Returns null if the operation failed or connection was lost.
         */
        public static byte[] Read(NetworkStream pStream)
        {
            return Read(pStream, DEFAULT_TIMEOUT_MS);
        }

        /**
         * Reads with custom timeout. Returns null if failed.
         */
        public static byte[] Read(NetworkStream pStream, int timeoutMs)
        {
            try
            {
                // Check if stream is still valid
                if (pStream == null || !pStream.CanRead)
                    return null;

                // Set read timeout
                pStream.ReadTimeout = timeoutMs;

                // Get the message size first (4 bytes)
                byte[] lengthBytes = Read(pStream, 4, timeoutMs);
                if (lengthBytes == null)
                    return null;

                int byteCountToRead = BitConverter.ToInt32(lengthBytes, 0);

                // Sanity check: prevent extremely large allocations
                if (byteCountToRead < 0 || byteCountToRead > 10 * 1024 * 1024) // 10MB limit
                {
                    Console.WriteLine($"Invalid message size: {byteCountToRead}");
                    return null;
                }

                // Then read that amount of bytes
                return Read(pStream, byteCountToRead, timeoutMs);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StreamUtil.Read failed: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        /**
         * Read the given amount of bytes from the stream with timeout support.
         * Returns null if the operation failed or didn't read the expected amount.
         */
        private static byte[] Read(NetworkStream pStream, int pByteCount, int timeoutMs)
        {
            if (pByteCount <= 0)
                return new byte[0];

            // Create a buffer to hold all the requested bytes
            byte[] bytes = new byte[pByteCount];
            int bytesRead = 0;
            int totalBytesRead = 0;

            try
            {
                pStream.ReadTimeout = timeoutMs;

                // Keep reading bytes until we've got what we are looking for or something bad happens
                while (totalBytesRead != pByteCount)
                {
                    bytesRead = pStream.Read(bytes, totalBytesRead, pByteCount - totalBytesRead);

                    // If we read 0 bytes, the connection was closed
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Connection closed while reading data");
                        return null;
                    }

                    totalBytesRead += bytesRead;
                }
            }
            catch (IOException ioEx)
            {
                // Handle specific IO exceptions (timeouts, connection issues)
                Console.WriteLine($"IO Exception while reading: {ioEx.Message}");
                return null;
            }
            catch (ObjectDisposedException)
            {
                // Stream was disposed
                Console.WriteLine("Stream was disposed while reading");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected exception while reading: {ex.GetType().Name} - {ex.Message}");
                return null;
            }

            return (totalBytesRead == pByteCount) ? bytes : null;
        }

        /**
         * Check if a NetworkStream is still usable for communication
         */
        public static bool IsStreamValid(NetworkStream stream)
        {
            try
            {
                return stream != null && stream.CanRead && stream.CanWrite;
            }
            catch
            {
                return false;
            }
        }

        /**
         * Safe way to close a NetworkStream without throwing exceptions
         */
        public static void SafeClose(NetworkStream stream)
        {
            try
            {
                stream?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing stream: {ex.Message}");
            }
        }
    }
}