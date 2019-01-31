using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace hu.jia.webapi3.Handlers
{
    public class CompressionHandler : DelegatingHandler
    {
        public CompressionHandler()
        {
            this.Compressors = new Collection<ICompressor>();
            this.Compressors.Add(new GZipCompressor());
            this.Compressors.Add(new DeflateCompressor());
            this.Compressors.Add(new LZ4Compressor());
        }

        /// <summary>
        /// Gets a collection of <see cref="ICompressor"/> that are registered.
        /// </summary>
        /// <value>
        /// The registered compressors.
        /// </value>
        public Collection<ICompressor> Compressors
        {
            get;
            private set;
        }

        /// <summary>
        /// Sends an HTTP request to the inner handler to send to the server as an asynchronous operation.
        /// </summary>
        /// <param name="request">The HTTP request message to send to the server.</param>
        /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
        /// <returns>
        /// Returns <see cref="T:System.Threading.Tasks.Task`1" />. The task object representing the asynchronous operation.
        /// </returns>
        protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            if (request.Content.Headers.ContentEncoding != null
                && request.Content.Headers.ContentEncoding.Any())
            {
                //// request content is compressed, decompress it.
                var encoding = request.Content.Headers.ContentEncoding.First();
                var compressor = this.Compressors.FirstOrDefault(c => c.EncodingType.Equals(encoding, StringComparison.InvariantCultureIgnoreCase));
                if (compressor != null)
                {
                    request.Content = new DecompressedHttpContent(request.Content, compressor);
                }
            }

            if (request.Headers.AcceptEncoding != null && request.Headers.AcceptEncoding.Any())
            {
                //// response needs to be compressed
                var response = await base.SendAsync(request, cancellationToken);

                if (response.Content == null)
                {   //// no content to be encoded.
                    return response;
                }

                var encoding = request.Headers.AcceptEncoding.First();
                var compressor = this.Compressors.FirstOrDefault(c => c.EncodingType.Equals(encoding.Value, StringComparison.InvariantCultureIgnoreCase));
                if (compressor != null)
                {
                    response.Content = new CompressedHttpContent(response.Content, compressor);
                }

                return response;
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    public abstract class CompressionHttpContent : HttpContent
    {
        /// <summary>
        /// The original content.
        /// </summary>
        private readonly HttpContent originalContent;

        /// <summary>
        /// The compressor.
        /// </summary>
        private readonly ICompressor compressor;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionHttpContent"/> class.
        /// </summary>
        /// <param name="content">The original HttpContent object.</param>
        /// <param name="compressor">The compressor.</param>
        public CompressionHttpContent(HttpContent content, ICompressor compressor)
        {
            this.originalContent = content;
            this.compressor = compressor;

            this.SetContentHeaders();
        }

        /// <summary>
        /// Gets the <see cref="ICompressor"/>.
        /// </summary>
        /// <value>
        /// The compressor.
        /// </value>
        protected ICompressor Compressor
        {
            get
            {
                return this.compressor;
            }
        }

        /// <summary>
        /// Gets the original <see cref="HttpContent"/>.
        /// </summary>
        /// <value>
        /// The original <see cref="HttpContent"/>.
        /// </value>
        protected HttpContent OriginalContent
        {
            get
            {
                return this.originalContent;
            }
        }

        /// <summary>
        /// Determines whether the HTTP content has a valid length in bytes.
        /// </summary>
        /// <param name="length">The length in bytes of the HHTP content.</param>
        /// <returns>
        /// Returns <see cref="T:System.Boolean" />.true if <paramref name="length" /> is a valid length; otherwise, false.
        /// </returns>
        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        /// <summary>
        /// The set content headers.
        /// </summary>
        private void SetContentHeaders()
        {
            //// copy headers from original content
            foreach (var header in this.originalContent.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            //// add the content encoding header
            Headers.ContentEncoding.Add(this.compressor.EncodingType);
        }
    }

    public class DecompressedHttpContent : CompressionHttpContent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DecompressedHttpContent"/> class.
        /// </summary>
        /// <param name="content">The original HttpContent object.</param>
        /// <param name="compressor">The compressor.</param>
        public DecompressedHttpContent(HttpContent content, ICompressor compressor) : base(content, compressor)
        {
        }

        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be null.</param>
        /// <returns>
        /// Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.
        /// </returns>
        protected override Task SerializeToStreamAsync(System.IO.Stream stream, System.Net.TransportContext context)
        {
            Stream compressionStream = this.Compressor.CreateDecompressionStream(this.OriginalContent.ReadAsStreamAsync().Result);

            return compressionStream.CopyToAsync(stream).ContinueWith(task =>
            {
                if (compressionStream != null)
                {
                    compressionStream.Dispose();
                }
            });
        }
    }

    public class CompressedHttpContent : CompressionHttpContent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompressedHttpContent"/> class.
        /// </summary>
        /// <param name="content">The original HttpContent object.</param>
        /// <param name="compressor">The compressor.</param>
        public CompressedHttpContent(HttpContent content, ICompressor compressor)
            : base(content, compressor)
        {
        }

        /// <summary>
        /// Serialize the HTTP content to a stream as an asynchronous operation.
        /// </summary>
        /// <param name="stream">The target stream.</param>
        /// <param name="context">Information about the transport (channel binding token, for example). This parameter may be null.</param>
        /// <returns>
        /// Returns <see cref="T:System.Threading.Tasks.Task" />.The task object representing the asynchronous operation.
        /// </returns>
        protected override Task SerializeToStreamAsync(System.IO.Stream stream, System.Net.TransportContext context)
        {
            Stream compressionStream = this.Compressor.CreateCompressionStream(stream);

            return this.OriginalContent.CopyToAsync(compressionStream).ContinueWith(task =>
            {
                if (compressionStream != null)
                {
                    compressionStream.Dispose();
                }
            });
        }
    }

    public interface ICompressor
    {
        string EncodingType { get; }
        Stream CreateCompressionStream(Stream output);
        Stream CreateDecompressionStream(Stream input);
    }

    public class GZipCompressor : ICompressor
    {
        /// <summary>
        /// The g zip encoding.
        /// </summary>
        private const string GZipEncoding = "gzip";

        /// <summary>
        /// Gets the type of the encoding.
        /// </summary>
        /// <value>
        /// The type of the encoding.
        /// </value>
        public string EncodingType
        {
            get
            {
                return GZipEncoding;
            }
        }

        /// <summary>
        /// Creates the compression stream.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <returns>
        /// A Compression Stream.
        /// </returns>
        public Stream CreateCompressionStream(Stream output)
        {
            return new GZipStream(output, CompressionMode.Compress, true);
        }

        /// <summary>
        /// Creates the decompression stream.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>
        /// A Decompression Stream.
        /// </returns>
        public Stream CreateDecompressionStream(Stream input)
        {
            return new GZipStream(input, CompressionMode.Decompress, true);
        }
    }

    public class DeflateCompressor : ICompressor
    {
        /// <summary>
        /// The deflate compression.
        /// </summary>
        private const string DeflateCompression = "deflate";

        /// <summary>
        /// Gets the type of the encoding.
        /// </summary>
        /// <value>
        /// The type of the encoding.
        /// </value>
        public string EncodingType
        {
            get
            {
                return DeflateCompression;
            }
        }

        /// <summary>
        /// Creates the compression stream.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <returns>
        /// A Compression Stream.
        /// </returns>
        public Stream CreateCompressionStream(Stream output)
        {
            return new DeflateStream(output, CompressionMode.Compress, true);
        }

        /// <summary>
        /// Creates the decompression stream.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>
        /// A Decompression Stream.
        /// </returns>
        public Stream CreateDecompressionStream(Stream input)
        {
            return new DeflateStream(input, CompressionMode.Decompress, true);
        }
    }

    public class LZ4Compressor : ICompressor
    {
        /// <summary>
        /// The lz4 zip encoding.
        /// </summary>
        private const string LZ4Encoding = "lz4";

        /// <summary>
        /// Gets the type of the encoding.
        /// </summary>
        /// <value>
        /// The type of the encoding.
        /// </value>
        public string EncodingType
        {
            get
            {
                return LZ4Encoding;
            }
        }

        /// <summary>
        /// Creates the compression stream.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <returns>
        /// A Compression Stream.
        /// </returns>
        public Stream CreateCompressionStream(Stream output)
        {
            byte[] buffer = new byte[1024];

            lz4.LZ4Stream stream = lz4.LZ4Stream.CreateCompressor(output, lz4.LZ4StreamMode.Write, lz4.LZ4FrameBlockMode.Linked, lz4.LZ4FrameBlockSize.Max64KB, lz4.LZ4FrameChecksumMode.Content, null, false);
            // write uncompressed data to the lz4 stream
            // the stream will compress the data and write it to the innerStream
            stream.Write(buffer, 0, buffer.Length);

            return stream;
        }

        /// <summary>
        /// Creates the decompression stream.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>
        /// A Decompression Stream.
        /// </returns>
        public Stream CreateDecompressionStream(Stream input)
        {
            byte[] buffer = new byte[1024];

            lz4.LZ4Stream stream = lz4.LZ4Stream.CreateDecompressor(input, lz4.LZ4StreamMode.Write, false);
            // the lz4 stream will decompress the data from 'buffer'
            // and write the uncompressed data to the 'innerStream'
            stream.Write(buffer, 0, buffer.Length);

            return stream;
        }
    }
}