using System;
using System.ComponentModel;
using System.Text;
using Sprache;

namespace Grammar
{
    public static class ExpressionParser
    {
        private static readonly Parser<string> Field =
            Sprache.Parse
                .Identifier(Sprache.Parse
                    .Letter, Sprache.Parse
                        .LetterOrDigit
                        .XOr(Sprache.Parse
                            .Char('_'))
                        .XOr(Sprache.Parse
                            .Char('-')))
                .Token();


        private static readonly Parser<Operator> Equal = Operator(":=", Grammar.Operator.Equals)
            .Or(Operator(":", Grammar.Operator.Equals))
            .Or(Operator("=", Grammar.Operator.Equals));

        private static readonly Parser<Operator> NotEqual = Operator("!=", Grammar.Operator.NotEqual)
            .Or(Operator("<>", Grammar.Operator.NotEqual));

        private static readonly Parser<Operator> GreaterThan = Operator(">", Grammar.Operator.GreaterThan);
        private static readonly Parser<Operator> GreaterOrEqual = Operator(">=", Grammar.Operator.GreaterOrEqual);

        private static readonly Parser<Operator> LessThan = Operator("<", Grammar.Operator.LessThan);
        private static readonly Parser<Operator> LessOrEqual = Operator("<=", Grammar.Operator.LessOrEqual);

        //Comporator: ':=' | ':' | '=' | '!=' | '<>' | '>=' | '<=' | '>' | '<' 
        // NOTE: ORDER MATTERS, since otherwise shorter comporator will be recognized before longer ones!
        private static readonly Parser<Operator> Comporator =
            Equal
                .Or(NotEqual)
                .Or(LessOrEqual)
                .Or(GreaterOrEqual)
                .Or(GreaterThan)
                .Or(LessThan)
                .Named("Comporator");

        private static readonly Parser<char> OpenParenthesis =
            Sprache.Parse
                .Char('(')
                .Token();

        private static readonly Parser<char> CloseParenthesis =
            Sprache.Parse
                .Char(')')
                .Token();

        private static readonly Parser<char> Negative =
            Sprache.Parse
                .Char('-');

        public static readonly Parser<long> Number =
            from sign in Negative.Optional()
            from number in Sprache.Parse.Number
            select long.Parse(sign.IsEmpty ? number : sign.Get() + number);

        public static readonly Parser<string> Token =
            Sprache.Parse
                .LetterOrDigit
                .AtLeastOnce()
                .Text()
                .Named(nameof(Token));

        public static readonly Parser<string> QuotedString =
            DelimitedBy('\'')
                .Or(DelimitedBy('\"'))
                .Named(nameof(QuotedString));

        //Value: Number | Token | String
        private static readonly Parser<TypedValue> Value =
            Number.Select(n => new NumberValue(n))
                .Or<TypedValue>(QuotedString.Select(s => new QuotedStringValue(s)))
                .Or<TypedValue>(Token.Select(t => new TokenValue(t)))
                .Token()
                .Named(nameof(Value));

        private static readonly Parser<Operator> Logic = Operator("and", Grammar.Operator.And)
            .Or(Operator("or", Grammar.Operator.Or));

        //term:
        //  '(' expr ')' |
        //  field COMPORATOR value
        private static readonly Parser<Node> InnerExpression =
            Sprache.Parse
                .Ref(() => Expression)
                .Contained(OpenParenthesis, CloseParenthesis)
                .Select(n => new InnerNode(n))
                .Named(nameof(InnerExpression));

        private static readonly Parser<Node> Term =
            (from id in Field
                from comporator in Comporator
                from value in Value
                select new ComparisonNode(id, comporator, value))
                .Or(InnerExpression)
                .Token()
                .Named(nameof(Term));


        //expr:
        //  term |
        // expr LOGIC expr
        private static readonly Parser<Node> Expression = Sprache.Parse.ChainOperator(Logic,
            Term, (op, left, right) => new LogicNode(op, left, right));


        public static Node Parse(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                throw new ArgumentNullException(nameof(input));
            }

            return Expression.End().Parse(input);
        }

        private static Parser<Operator> Operator(string op, Operator opType)
        {
            return Sprache.Parse.IgnoreCase(op).Token().Return(opType);
        }

        private static Parser<string> DelimitedBy(char delimiter)
        {
            return Sprache.Parse
                .CharExcept(delimiter)
                .AtLeastOnce()
                .Text()
                .Contained(Sprache.Parse.Char(delimiter), Sprache.Parse.Char(delimiter));
        }
    }


    public enum Operator
    {
        #region Comporator

        Equals,
        NotEqual,
        GreaterThan,
        LessThan,
        LessOrEqual,
        GreaterOrEqual,

        #endregion

        #region Logical

        And,
        Or

        #endregion
    }

    public abstract class TypedValue
    {
    }

    public abstract class TypedValue<T> : TypedValue
    {
        public T Value { get; }

        protected TypedValue(T value)
        {
            Value = value;
        }
    }

    public sealed class TokenValue : TypedValue<string>
    {
        public TokenValue(string value) : base(value)
        {
        }
    }

    public sealed class NumberValue : TypedValue<long>
    {
        public NumberValue(long value) : base(value)
        {
        }
    }

    public sealed class QuotedStringValue : TypedValue<string>
    {
        public QuotedStringValue(string value) : base(value)
        {
        }
    }

    public abstract class Node
    {

    }

    public interface INodeVisitor
    {
        void Visit(LogicNode logicNode);
        void Visit(InnerNode innerNode);
        void Visit(ComparisonNode comparisonNode);
    }

    public class SqlNodeVisitor: INodeVisitor
    {
        private StringBuilder _writer;
        private readonly string _space = " ";
        public SqlNodeVisitor()
        {
            _writer = new StringBuilder();
        }

        public void Visit(LogicNode logicNode)
        {
            Visit((dynamic)logicNode.Left);
            _writer.Append(_space).Append(OperatorToString(logicNode.Logic)).Append(_space);
            Visit((dynamic)logicNode.Right);
        }

        public void Visit(InnerNode innerNode)
        {
            _writer.Append("(");
            Visit((dynamic)innerNode.Inner);
            _writer.Append(")");
        }

        public void Visit(ComparisonNode comparisonNode)
        {
            _writer.Append($"{comparisonNode.Field}{OperatorToString(comparisonNode.Comporator)}{GetValue(comparisonNode.Value)}");
        }

        private string OperatorToString(Operator op)
        {
            switch (op)
            {
                case Operator.Equals:
                    return "=";
                case Operator.NotEqual:
                    return "<>";
                case Operator.GreaterThan:
                    return ">";
                case Operator.LessThan:
                    return "<";
                case Operator.LessOrEqual:
                    return "<=";
                case Operator.GreaterOrEqual:
                    return ">=";
                case Operator.And:
                    return "and";
                case Operator.Or:
                    return "or";
                default:
                    throw new ArgumentOutOfRangeException(nameof(op), op, null);
            }
        }

        private object GetValue(TypedValue value)
        {
            var number =  value as NumberValue;
            if (number != null)
            {
                return number.Value;
            }

            var token = value as TokenValue;
            if (token != null)
            {
                return QuoteString(token.Value);
            }

            var quotedString = value as QuotedStringValue;
            if (quotedString != null)
            {
                return QuoteString(quotedString.Value);
            }

            throw new ArgumentOutOfRangeException(nameof(value), value, null);
        }

        private string QuoteString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            string doubleQuote = "\"";
            return string.Concat(doubleQuote, value, doubleQuote);
        }

        public override string ToString()
        {
            return $"{_writer}";
        }
    }

    public sealed class InnerNode : Node
    {
        public Node Inner { get; }

        public InnerNode(Node inner)
        {
            Inner = inner;
        }
    }

    public sealed class LogicNode : Node
    {
        public Operator Logic { get; }
        public Node Left { get; }
        public Node Right { get; }

        public LogicNode(Operator logic, Node left, Node right)
        {
            Logic = logic;
            Left = left;
            Right = right;
        }
    }

    public sealed class ComparisonNode : Node
    {
        public string Field { get; }
        public Operator Comporator { get; }
        public TypedValue Value { get; }

        public ComparisonNode(string field, Operator comporator, TypedValue value)
        {
            Field = field;
            Comporator = comporator;
            Value = value;
        }
    }
}