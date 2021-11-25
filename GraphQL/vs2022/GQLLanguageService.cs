using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
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
        public bool ShowNotificationOnInitializeFailed => true;

#if DEBUG
        public static string GetCompilePath([CallerFilePath] string path = null) => path;
        public static string BaseDir => Path.GetDirectoryName(Path.GetDirectoryName(GetCompilePath()));

#else
        public static string BaseDir => null;
#endif

        public static Guid OutputPaneGuid = new Guid("2727c0ae-4ab2-4bd7-abd0-a9e6800c068a");

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            await Task.Yield();

            try
            {
                ProcessStartInfo info = new ProcessStartInfo();
                if (useBundle)
                {
                    info.FileName = "node.exe";
                    info.Arguments = "\"" + Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "bundle.js") + "\"";
                }
                else
                {
                    info.FileName = "cmd.exe";
                    info.Arguments = "/c ts-node " + Path.Combine(BaseDir, "node", "index.ts");
                }
                if (!debug)
                    info.CreateNoWindow = true;
                info.RedirectStandardInput = true;
                info.RedirectStandardOutput = true;
                info.UseShellExecute = false;
                var process = Process.Start(info);
                if (debug)
                {
                    var log = Path.Combine(BaseDir, "log.txt");
                    var logStream = new FileStream(log, FileMode.Create, FileAccess.Write);
                    var initMessage = Encoding.UTF8.GetBytes($"Begin {DateTime.Now}\r\n");
                    await logStream.WriteAsync(initMessage, 0, initMessage.Length);
                    return new Connection(new TeeStream(process.StandardOutput.BaseStream, logStream),
                        new TeeStream(process.StandardInput.BaseStream, logStream));
                }
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }
            catch (Exception e)
            {
                LogError(e);
                throw;
            }
        }

        public async Task OnLoadedAsync()
        {
            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            LogError(initializationState.InitializationException);
            return Task.FromResult(new InitializationFailureContext { FailureMessage = initializationState.StatusMessage });
        }

        private void LogError(Exception e)
        {
            var outWindow = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;

            var windowTitle = this.Name;
            outWindow.CreatePane(ref OutputPaneGuid, windowTitle, 1, 1);
            outWindow.GetPane(ref OutputPaneGuid, out var customPane);

            var messages = new[]
            {
                $"GraphQL Server initialization failed." + e
            };
            customPane.OutputString(String.Join(Environment.NewLine, messages));
            customPane.Activate(); // brings the pane into view
        }

        public Task AttachForCustomMessageAsync(JsonRpc rpc)
        {
            return Task.CompletedTask;
        }
    }
}
