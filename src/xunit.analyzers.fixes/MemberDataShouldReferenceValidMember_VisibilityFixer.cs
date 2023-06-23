using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Xunit.Analyzers.CodeActions;

namespace Xunit.Analyzers.Fixes;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public sealed class MemberDataShouldReferenceValidMember_VisibilityFixer : BatchedMemberFixProvider
{
	const string title = "Make Member Public";

	public MemberDataShouldReferenceValidMember_VisibilityFixer() :
		base(Descriptors.X1016_MemberDataMustReferencePublicMember.Id)
	{ }

	public override Task RegisterCodeFixesAsync(
		CodeFixContext context,
		ISymbol member)
	{
		context.RegisterCodeFix(
			CodeAction.Create(
				title: title,
				createChangedSolution: ct => context.Document.Project.Solution.ChangeMemberAccessibility(member, Accessibility.Public, ct),
				equivalenceKey: title
			),
			context.Diagnostics
		);

		return Task.CompletedTask;
	}
}
