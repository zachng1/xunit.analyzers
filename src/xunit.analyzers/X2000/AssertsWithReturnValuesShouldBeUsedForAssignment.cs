using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Xunit.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AssertsWithReturnValuesShouldBeUsedForAssignment : AssertUsageAnalyzerBase
{
	static readonly string[] targetMethods =
	{
		Constants.Asserts.Collection,
		Constants.Asserts.Single
	};
	
	public AssertsWithReturnValuesShouldBeUsedForAssignment() : base(Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment, targetMethods)
	{
	}

	protected override void AnalyzeInvocation(OperationAnalysisContext context, XunitContext xunitContext,
		IInvocationOperation invocationOperation, IMethodSymbol method)
	{
		Guard.ArgumentNotNull(xunitContext);
		Guard.ArgumentNotNull(invocationOperation);
		Guard.ArgumentNotNull(method);

		if (method.Name != Constants.Asserts.Single)
		{
			return;
		}

		var semanticModel = invocationOperation.SemanticModel;
		if (semanticModel is null)
		{
			return;
		}
		var nodeWithInvocation = invocationOperation.Syntax;
		
		
		var objectBeingAssertedOn = method.Parameters[0]; // will have to make this a sensible value for things that take more than 1 param
		// find uses of "collection" within siblings and descendants

		// need to find usages after this one, rather than all within scope
		
		// todo: currently this only finds assignments to existing vars, not declaration and assignment - need to rewrite or expand for that.
		var usageOfSingleAssignmentExpressionWithOurObject = nodeWithInvocation
				.Ancestors()
				.OfType<BlockSyntax>() //lowest level is just this scope
				.First()
				//.Where(syntaxNode => syntaxNode.SpanStart > nodeWithInvocation.Span.End) // only want siblings after this call (wait - do we? can think about)
				//lowest level is just this scope
				.ChildNodes()
				//.Where(syntaxNode => syntaxNode.SpanStart > nodeWithInvocation.Span.End) // only want siblings after this call (wait - do we? can think about)
				.OfType<ExpressionStatementSyntax>()
				.Select(syntax => syntax.Expression) // get all assignments where the right operands are a member access expression where our object gets Single() called on it
				.OfType<AssignmentExpressionSyntax>()
				.First(syntax => syntax.Right.ChildNodes().OfType<MemberAccessExpressionSyntax>().Any(syntaxNode =>
				{
					var nodeAsString = syntaxNode.ToFullString();
					return nodeAsString.Contains(objectBeingAssertedOn.Name) && nodeAsString.Contains("Single");
				}))
			;
		var usageOfSingleAssignmentExpressionWithOurObjectAsSymbol =
			semanticModel.GetSymbolInfo(usageOfSingleAssignmentExpressionWithOurObject.Right);
		
		// okay, we have identified our problematic positions
		// for a fix, we need to replace these with the call to XUnit
		
		// roslyn might have something that will find all references
		// if it's just within a single scope - search for uses of the LINQ single within the same scope, and check for the name of the symbol the LINQ single is being called on 
		// check type as well perhaps
		// might need to use semantic model over syntax tree because we need to know if it's the same type
		// find symbol info in semantic model
		
		// general advice - the API is way more gnarly than expected: try make helper methods at abstracted level - i.e. get containing type
		// look at autofix.moq in xero internal


		if (usageOfSingleAssignmentExpressionWithOurObjectAsSymbol.Symbol != null)
			context.ReportDiagnostic(
				Diagnostic.Create(
					Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment,
					usageOfSingleAssignmentExpressionWithOurObject.GetLocation(),
					SymbolDisplay.ToDisplayString(
						method,
						SymbolDisplayFormat
							.CSharpShortErrorMessageFormat
							.WithParameterOptions(SymbolDisplayParameterOptions.None)
							.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
					),
					SymbolDisplay.ToDisplayString(
						usageOfSingleAssignmentExpressionWithOurObjectAsSymbol.Symbol,
						SymbolDisplayFormat
							.CSharpShortErrorMessageFormat
							.WithParameterOptions(SymbolDisplayParameterOptions.None)
							.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
					),
					SymbolDisplay.ToDisplayString(
						method,
						SymbolDisplayFormat
							.CSharpShortErrorMessageFormat
							.WithParameterOptions(SymbolDisplayParameterOptions.None)
							.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
					)
				)
			);
	}
}
