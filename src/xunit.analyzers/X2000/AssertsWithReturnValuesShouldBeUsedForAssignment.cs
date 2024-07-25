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
			var collectionParameter = method.Parameters[0]; // will have to make this a sensible value for things that take more than 1 param
			ProcessXunitSingle(invocationNode, semanticModel, collectionParameter, context, method);
		}
	}

	private static void ProcessXunitSingle(SyntaxNode invocationNode, SemanticModel semanticModel,
		IParameterSymbol collectionSymbol,
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

		
		// could be improved, but currently just gets the nearest reference to the same collection
		var nearestReferenceToReplacementCandidate = invocationNode
				.Ancestors()
				.OfType<BlockSyntax>()
				.First() // we just want to find references within the containing scope
				.ChildNodes()
				.OfType<ExpressionStatementSyntax>()
				.Select(syntax => syntax.Expression)
				.OfType<AssignmentExpressionSyntax>()
				.FirstOrDefault(syntax =>
				{
					var symbol = semanticModel.GetSymbolInfo(syntax.Right).Symbol;
					if (symbol is IMethodSymbol methodSymbol) {
						var fullMethodName = methodSymbol.ReducedFrom?.ToDisplayString();
							if (fullMethodName is null) return false;
							if (fullMethodName == (string?)XunitMethodToReplacementCandidates[Constants.Asserts.Single])
							{
								return ((InvocationExpressionSyntax)syntax.Right).Expression.ChildNodes()
									.Any(node => SymbolEqualityComparer.Default.Equals(semanticModel.GetSymbolInfo(node).Symbol, collectionSymbol)); // if we have found a call to Linq's single, then check that one of the operands is the same collection
								// todo: these two symbols are not the same - what's a better way of comparing them? 
							} 
					}
					return false;
				})
			;
		// couldn't find any bad usages of 
		if (nearestReferenceToReplacementCandidate is null)
		{
			return;
		}
		
		
		var symbol = semanticModel.GetSymbolInfo(nearestReferenceToReplacementCandidate.Right).Symbol;
		if (symbol != null)
			context.ReportDiagnostic(
				Diagnostic.Create(
					Descriptors.X2030_AssertsWithReturnValuesShouldBeUsedForAssigment,
					nearestReferenceToReplacementCandidate.GetLocation(),
					SymbolDisplay.ToDisplayString(
						method,
						SymbolDisplayFormat
							.CSharpShortErrorMessageFormat
							.WithParameterOptions(SymbolDisplayParameterOptions.None)
							.WithGenericsOptions(SymbolDisplayGenericsOptions.None)
					),
					SymbolDisplay.ToDisplayString(
						symbol,
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
