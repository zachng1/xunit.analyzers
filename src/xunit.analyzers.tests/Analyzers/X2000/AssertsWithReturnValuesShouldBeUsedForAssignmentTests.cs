using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Analyzers;
using Verify = CSharpVerifier<Xunit.Analyzers.AssertsWithReturnValuesShouldBeUsedForAssignment>;

public class AssertsWithReturnValuesShouldBeUsedForAssignmentTests
{

	[Fact]
	public async Task AssertSingleFollowedByAssignment_Triggers()
	{
		var source = @"
		using System.Collections.Generic;
		using System.Linq;

		class TestClass {
			void TestMethod() {
				List<int> list;
				int item;
				list = new List<int> {1};
				Xunit.Assert.Single(list);
				item = list.Single(); // only this one
				item = list.Single();
			}
		}
		";
		await Verify.VerifyAnalyzer(source);
	}
}
