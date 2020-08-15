using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace GraphQL
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("graphql")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class GraphQLClassifierProvider : ITaggerProvider
    {

        [Export]
        [Name("graphql")]
        [BaseDefinition("code")]
        internal static ContentTypeDefinition GraphQLContentType = null;

        [Export]
        [FileExtension(".graphql")]
        [ContentType("graphql")]
        internal static FileExtensionToContentTypeDefinition GraphQLFileType = null;

        [Import]
        internal IClassificationTypeRegistryService ClassificationTypeRegistry = null;

        [Import]
        internal IBufferTagAggregatorFactoryService aggregatorFactory = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return (ITagger<T>)new Tagger(ClassificationTypeRegistry);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "mutation")]
    [Name("mutation")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class MutationDefinition : ClassificationFormatDefinition
    {
        public MutationDefinition()
        {
            DisplayName = "mutation"; 
            ForegroundColor = Colors.BlueViolet;
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "query")]
    [Name("query")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class QueryDefinition : ClassificationFormatDefinition
    {
        public QueryDefinition()
        {
            DisplayName = "query";
            ForegroundColor = Colors.BlueViolet;
        }
    }

    internal static class OrdinaryClassificationDefinition
    {
        [Export(typeof(ClassificationTypeDefinition))]
        [Name("query")]
        internal static ClassificationTypeDefinition query = null;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name("mutation")]
        internal static ClassificationTypeDefinition mutation = null;
    }

    class Tagger : ITagger<ClassificationTag>
    {
        private Dictionary<string, IClassificationType> types = new Dictionary<string, IClassificationType>();

        public Tagger(IClassificationTypeRegistryService typeService)
        {
            types["mutation"] = typeService.GetClassificationType("mutation");
            types["query"] = typeService.GetClassificationType("query");
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                int curLoc = containingLine.Start.Position;
                string[] tokens = containingLine.GetText().ToLower().Split(' ');

                foreach (string token in tokens)
                {
                    if (types.TryGetValue(token, out var value))
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, token.Length));
                        if (tokenSpan.IntersectsWith(curSpan))
                            yield return new TagSpan<ClassificationTag>(tokenSpan, new ClassificationTag(value));
                    }
                    curLoc += token.Length + 1;
                }
            }
        }
    }
}
