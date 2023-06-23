using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit.Analyzers.CodeActions;

namespace Xunit.Analyzers.Fixes;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public class TestClassShouldHaveTFixtureArgumentFixer : BatchedCodeFixProvider
{
	const string titleTemplate = "Generate constructor {0}({1})";

	public TestClassShouldHaveTFixtureArgumentFixer() :
		base(Descriptors.X1033_TestClassShouldHaveTFixtureArgument.Id)
	{ }

	public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is null)
			return;

		var classDeclaration = root.FindNode(context.Span).FirstAncestorOrSelf<ClassDeclarationSyntax>();
		if (classDeclaration is null)
			return;

		var diagnostic = context.Diagnostics.FirstOrDefault();
		if (diagnostic is null)
			return;
		if (!diagnostic.Properties.TryGetValue(Constants.Properties.TestClassName, out var testClassName))
			return;
		if (!diagnostic.Properties.TryGetValue(Constants.Properties.TFixtureName, out var tFixtureName))
			return;
		if (tFixtureName is null)
			return;

		if (!diagnostic.Properties.TryGetValue(Constants.Properties.TFixtureDisplayName, out var tFixtureDisplayName))
			return;
		if (tFixtureDisplayName is null)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: string.Format(titleTemplate, testClassName, tFixtureName),
				createChangedDocument: ct => context.Document.AddConstructor(
					classDeclaration,
					typeDisplayName: tFixtureDisplayName,
					typeName: tFixtureName,
					ct
				),
				equivalenceKey: titleTemplate
			),
			context.Diagnostics
		);
	}
}
