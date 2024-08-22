using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Xunit.Analyzers;


// todo: rename - Assert.Single can be used for assignment. Too big of a task to do for all 

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AssertsWithReturnValuesShouldBeUsedForAssignment : AssertUsageAnalyzerBase
{
	static readonly string[] targetMethods =
	{
		Constants.Asserts.Single
	};

	// map xunit methods to the extension methods that they can replace
	private static readonly Hashtable XunitMethodToReplacementCandidates = new()
	{
		{Constants.Asserts.Single, "System.Linq.Enumerable.Single<TSource>(System.Collections.Generic.IEnumerable<TSource>)"}
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

		var semanticModel = invocationOperation.SemanticModel;
		if (semanticModel is null)
		{
			return;
		}
		if (method.Name == Constants.Asserts.Single)
		{
			var invocationNode = invocationOperation.Syntax;
			if (invocationNode is InvocationExpressionSyntax syntax)
			{
				ProcessXunitSingle(syntax, semanticModel, context, method);

			}
		}
	}

	private static void ProcessXunitSingle(InvocationExpressionSyntax invocationNode, SemanticModel semanticModel,
		OperationAnalysisContext context, IMethodSymbol method)
	{
		// todo: currently this only finds assignments to existing vars, not 'declaration and assignment' - need to rewrite or expand for that.

		// clean 'steps':
		// find all references to the parameter to xunit.single (in this case 'collection').
		// find all those references in which that parameter is being used as part of a call to "System.Linq.Enumerable.Single"
		// ensure there are no references between the two which might change the collection
		//		this second can be done maybe by getting a list of all childnodes within the block
		//		then iterating over, finding those that refer to collection symbol - and if we hit any that edit the collection (calls to add, delete etc.), we return. keep going until we find the one which makes a call to Linq.Enumerable.Single() - then we exit with that as our replacement candidate.
		//		will have to do in both directions - first approaching from above -- if we don't find *any* calls to single above, then we iterate over after, looking for calls to single(). If we find one, we enter the mode of checking for changes to the collection. If we find a change, we go back to looking for calls to single(). If we find none before hitting our current node again (call to xunit.single), we start looking for calls to linq.single(). If we hit a collection changing method, we return.
		// YUK!  but that's the best algo i can think of

		var containingBlock = invocationNode
			.Ancestors()
			.OfType<BlockSyntax>()
			.First();

		var argumentToSingle = invocationNode.ArgumentList.Arguments.FirstOrDefault();
		if (argumentToSingle is null)
		{
			return;
		}
		var argumentSymbol =
			semanticModel.GetSymbolInfo(argumentToSingle.Expression).Symbol;
		if (argumentSymbol is null)
		{
			return;
		}

		// could be improved, but currently just gets the nearest reference to the same collection
		var referencesToReplacementCandidates = invocationNode
			.Ancestors()
			.OfType<BlockSyntax>()
			.First() // we just want to find references within the containing scope
			.ChildNodes() // all sibling assignment expressions
			.OfType<ExpressionStatementSyntax>()
			.Select(syntax => syntax.Expression)
			.OfType<
				AssignmentExpressionSyntax>() // only want cases where something is being assigned (TODO this only gets assignment to existing var (rather than to initialise and assign)
			.Where(syntax =>
			{
				var symbol = semanticModel.GetSymbolInfo(syntax.Right).Symbol;
				if (symbol is not IMethodSymbol methodSymbol) return false;
				var fullMethodName = methodSymbol.ReducedFrom?.ToDisplayString();
				if (fullMethodName is null) return false;
				if (fullMethodName != (string?)XunitMethodToReplacementCandidates[Constants.Asserts.Single])
					return false;
				if (syntax.Right is not InvocationExpressionSyntax linqSingleInvocation ||
				    linqSingleInvocation.Expression is not MemberAccessExpressionSyntax
					    linqSingleMemberAccessExpression) return false;
				
				// expression property of member access is the object the member belongs to
				var nodeSymbol = semanticModel.GetSymbolInfo(linqSingleMemberAccessExpression.Expression).Symbol;
				if (nodeSymbol is null) return false;
				return SymbolEqualityComparer.Default.Equals(
					nodeSymbol, argumentSymbol);

			}).ToList();
		
		if (referencesToReplacementCandidates.Count == 0)
		{
			return;
		}

		foreach (var assignmentToCollection in referencesToReplacementCandidates)
		{
			var symbol = semanticModel.GetSymbolInfo(assignmentToCollection.Right).Symbol;
			if (symbol != null)
			{
				context.ReportDiagnostic(
					Diagnostic.Create(
						Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment,
						assignmentToCollection.GetLocation(),
						SymbolDisplay.ToDisplayString(
							symbol,
							SymbolDisplayFormat
								.CSharpShortErrorMessageFormat
								.WithParameterOptions(SymbolDisplayParameterOptions.None)
								.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
						),
						Constants.Asserts.Single
					)
				);
			}
		}
	}

}
