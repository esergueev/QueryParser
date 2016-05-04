using System;
using Xunit;
using Xunit.Abstractions;

namespace Grammar.Tests
{
    public class Parsing
    {
        private readonly ITestOutputHelper _outputHelper;

        public Parsing(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }


        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void EmptyInputShouldThrowException(string input)
        {
            Assert.Throws<ArgumentNullException>(() => ExpressionParser.Parse(input));
        }


        [Theory]
        [InlineData("(f1<1 and f2>\"2\") and ((f3<>token and f4:=to4e) or (f5>=1 and f6<= 10 ))")]
        public void ShouldParseComplexExpression(string input)
        {
            //(inner_f1_f2) and (inner(inner_f3_f4 or inner_f5_f5) )
            var sut = Assert.IsType<LogicNode>(ExpressionParser.Parse(input));

            Assert.NotNull(sut);
            Assert.Equal(Operator.And, sut.Logic);

            //f1 and f2
            var leftInner = Assert.IsType<InnerNode>(sut.Left);

            var leftInnerLogical = Assert.IsType<LogicNode>(leftInner.Inner);
            Assert.Equal(Operator.And, leftInnerLogical.Logic);

            var f1ValueNode = Assert.IsType<ComparisonNode>(leftInnerLogical.Left);
            Assert.Equal("f1", f1ValueNode.Field);
            Assert.Equal(Operator.LessThan, f1ValueNode.Comporator);
            var f1Value = Assert.IsType<NumberValue>(f1ValueNode.Value);
            Assert.Equal(1, f1Value.Value);

            var f2ValueNode = Assert.IsType<ComparisonNode>(leftInnerLogical.Right);
            Assert.Equal("f2", f2ValueNode.Field);
            Assert.Equal(Operator.GreaterThan, f2ValueNode.Comporator);
            var f2Value = Assert.IsType<QuotedStringValue>(f2ValueNode.Value);
            Assert.Equal("2", f2Value.Value);

            //((inner_3_4) or (inner_5_6))
            var rightInner = Assert.IsType<InnerNode>(sut.Right);
            var rightInnerLogical = Assert.IsType<LogicNode>(rightInner.Inner);
            Assert.Equal(Operator.Or, rightInnerLogical.Logic);

            //f3 and f4
            var f3F4Inner = Assert.IsType<InnerNode>(rightInnerLogical.Left);
            var f3F4InnerLogical = Assert.IsType<LogicNode>(f3F4Inner.Inner);
            Assert.Equal(Operator.And, f3F4InnerLogical.Logic);

            var f3 = Assert.IsType<ComparisonNode>(f3F4InnerLogical.Left);
            Assert.Equal("f3", f3.Field);
            Assert.Equal(Operator.NotEqual, f3.Comporator);
            var f3Value = Assert.IsType<TokenValue>(f3.Value);
            Assert.Equal("token", f3Value.Value);

            var f4 = Assert.IsType<ComparisonNode>(f3F4InnerLogical.Right);
            Assert.Equal("f4", f4.Field);
            Assert.Equal(Operator.Equals, f4.Comporator);
            var f4Value = Assert.IsType<TokenValue>(f4.Value);
            Assert.Equal("to4e", f4Value.Value);

            //f5 and f6
            var f5F5Inner = Assert.IsType<InnerNode>(rightInnerLogical.Right);
            var f5F6InnerLogical = Assert.IsType<LogicNode>(f5F5Inner.Inner);
            Assert.Equal(Operator.And, f5F6InnerLogical.Logic);

            var f5 = Assert.IsType<ComparisonNode>(f5F6InnerLogical.Left);
            Assert.Equal("f5", f5.Field);
            Assert.Equal(Operator.GreaterOrEqual, f5.Comporator);
            var f5Value = Assert.IsType<NumberValue>(f5.Value);
            Assert.Equal(1, f5Value.Value);

            var f6 = Assert.IsType<ComparisonNode>(f5F6InnerLogical.Right);
            Assert.Equal("f6", f6.Field);
            Assert.Equal(Operator.LessOrEqual, f6.Comporator);
            var f6Value = Assert.IsType<NumberValue>(f6.Value);
            Assert.Equal(10, f6Value.Value);
        }

        [Theory]
        [InlineData("(f1<1 and f2>\"2\") and ((f3<>token and f4:=to4e) or (f5>=1 and f6<= 10 ))",
            "(f1<1 and f2>\"2\") and ((f3<>\"token\" and f4=\"to4e\") or (f5>=1 and f6<=10))")]
        [InlineData("f1 = 100", "f1=100")]
        [InlineData("f1 : token", "f1=\"token\"")]
        [InlineData("f1 :=\"quoted string \"", "f1=\"quoted string \"")]
        [InlineData("f1 :=100 and f2  >10", "f1=100 and f2>10")]
        [InlineData("f1 :=100 and (f2>tok3n)", "f1=100 and (f2>\"tok3n\")")]
        public void SqlVisitorShouldGenerateProperOutput(string input, string output)
        {
            var node = ExpressionParser.Parse(input);
            var sut = new SqlNodeVisitor();

            sut.Visit((dynamic) node);

            Assert.Equal(output, sut.ToString());
        }

        [Theory]

        #region NumberValue

        [InlineData("f1 = 100", "f1", Operator.Equals, 100L, typeof (NumberValue))]
        [InlineData("f1 : 100", "f1", Operator.Equals, 100L, typeof (NumberValue))]
        [InlineData("f1 := 100", "f1", Operator.Equals, 100L, typeof (NumberValue))]
        [InlineData("f1 <> 100", "f1", Operator.NotEqual, 100L, typeof (NumberValue))]
        [InlineData("f1 != 100", "f1", Operator.NotEqual, 100L, typeof (NumberValue))]
        [InlineData("f1 > 100", "f1", Operator.GreaterThan, 100L, typeof (NumberValue))]
        [InlineData("f1 >= 100", "f1", Operator.GreaterOrEqual, 100L, typeof (NumberValue))]
        [InlineData("f1 < 100", "f1", Operator.LessThan, 100L, typeof (NumberValue))]
        [InlineData("f1 <= 100", "f1", Operator.LessOrEqual, 100L, typeof (NumberValue))]

        #endregion

        #region TokenValue

        [InlineData("f1 = tok3n", "f1", Operator.Equals, "tok3n", typeof (TokenValue))]
        [InlineData("f1 : tok3n", "f1", Operator.Equals, "tok3n", typeof (TokenValue))]
        [InlineData("f1 := tok3n", "f1", Operator.Equals, "tok3n", typeof (TokenValue))]
        [InlineData("f1 <> tok3n", "f1", Operator.NotEqual, "tok3n", typeof (TokenValue))]
        [InlineData("f1 != tok3n", "f1", Operator.NotEqual, "tok3n", typeof (TokenValue))]
        [InlineData("f1 > tok3n", "f1", Operator.GreaterThan, "tok3n", typeof (TokenValue))]
        [InlineData("f1 >= tok3n", "f1", Operator.GreaterOrEqual, "tok3n", typeof (TokenValue))]
        [InlineData("f1 < tok3n", "f1", Operator.LessThan, "tok3n", typeof (TokenValue))]
        [InlineData("f1 <= tok3n", "f1", Operator.LessOrEqual, "tok3n", typeof (TokenValue))]

        #endregion

        #region QuotedStringValue

        [InlineData("f1 = \"quoted string\"", "f1", Operator.Equals, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 : \"quoted string\"", "f1", Operator.Equals, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 := \"quoted string\"", "f1", Operator.Equals, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 <> \"quoted string\"", "f1", Operator.NotEqual, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 != \"quoted string\"", "f1", Operator.NotEqual, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 > \"quoted string\"", "f1", Operator.GreaterThan, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 >= \"quoted string\"", "f1", Operator.GreaterOrEqual, "quoted string",
            typeof (QuotedStringValue))]
        [InlineData("f1 < \"quoted string\"", "f1", Operator.LessThan, "quoted string", typeof (QuotedStringValue))]
        [InlineData("f1 <= \"quoted string\"", "f1", Operator.LessOrEqual, "quoted string", typeof (QuotedStringValue))]

        #endregion

        public void ShouldParseSimpleExpression(string input, string field, Operator op, object val, Type type)
        {
            var sut = Assert.IsType<ComparisonNode>(ExpressionParser.Parse(input));

            Assert.NotNull(sut);
            Assert.Equal(field, sut.Field);
            Assert.Equal(op, sut.Comporator);
            Assert.IsType(type, sut.Value);
            dynamic value = Convert.ChangeType(sut.Value, type);
            Assert.Equal(val, value.Value);
        }
    }
}