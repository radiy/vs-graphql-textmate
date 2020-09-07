using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GraphQL
{
    public class GQLContentDefinition
    {
        [Export]
        [Name("graphql")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition GraphQLContentTypeDefinition;


        [Export]
        [FileExtension(".graphql")]
        [ContentType("graphql")]
        internal static FileExtensionToContentTypeDefinition GraphQLFileExtensionDefinition;
    }

    public class TeeStream : Stream
    {
        private Stream source;
        private Stream target;

        public TeeStream(Stream source, Stream target)
        {
            this.source = source;
            this.target = target;
        }
        public override bool CanRead => source.CanRead;

        public override bool CanSeek => source.CanSeek;

        public override bool CanWrite => source.CanWrite;

        public override long Length => source.Length;

        public override long Position { get => source.Position; set => source.Position = value; }

        public override void Flush()
        {
            source.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var result = source.Read(buffer, offset, count);
            target.Write(buffer, offset, result);
            target.Flush();
            return result;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return source.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            source.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            target.Write(buffer, offset, count);
            target.Flush();
            source.Write(buffer, offset, count);
        }
    }

    [ContentType("graphql")]
    [Export(typeof(ILanguageClient))]
    public class GQLLanguageService : ILanguageClient
    {
#if DEBUG
        private bool debug = true;
        private bool useBundle = true;
#else
        private bool debug = false;
        private bool useBundle = true;
#endif
        public string Name => "GraphQL Language Extension";

        public IEnumerable<string> ConfigurationSections => null;

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        public static string GetCompilePath([CallerFilePath] string path = null) => path;

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            await Task.Yield();

            var baseDir = Path.GetDirectoryName(Path.GetDirectoryName(GetCompilePath()));
            ProcessStartInfo info = new ProcessStartInfo();
            if (useBundle)
            {
                info.FileName = "node.exe";
                info.Arguments = "\"" + Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "bundle.js") + "\"";
            }
            else
            {
                info.FileName = "cmd.exe";
                info.Arguments = "/c ts-node " + Path.Combine(baseDir, "node", "index.ts");
            }
            if (debug)
                info.CreateNoWindow = true;
            info.WorkingDirectory = Path.Combine(baseDir, "test");
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            var process = Process.Start(info);
            if (debug)
            {
                var log = Path.Combine(baseDir, "log.txt");
                var logStream = new FileStream(log, FileMode.Create, FileAccess.Write);
                var initMessage = Encoding.UTF8.GetBytes($"Begin {DateTime.Now}\r\n");
                await logStream.WriteAsync(initMessage, 0, initMessage.Length);
                return new Connection(new TeeStream(process.StandardOutput.BaseStream, logStream), new TeeStream(process.StandardInput.BaseStream, logStream));
            }
            return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
        }

        public async Task OnLoadedAsync()
        {
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task OnServerInitializeFailedAsync(Exception e)
        {
            return Task.CompletedTask;
        }
    }
}
