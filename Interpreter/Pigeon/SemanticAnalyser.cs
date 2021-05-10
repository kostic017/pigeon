﻿using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Kostic017.Pigeon.Errors;
using Kostic017.Pigeon.Symbols;
using Antlr4.Runtime.Tree;
using Kostic017.Pigeon.Operators;
using System.Collections.Generic;

namespace Kostic017.Pigeon
{
    public class SemanticAnalyser : PigeonBaseListener
    {
        internal CodeErrorBag ErrorBag { get; } = new CodeErrorBag();

        public CodeError[] Errors
        {
            get
            {
                return ErrorBag.Errors;
            }
        }

        Scope scope;
        readonly Scope globalScope = new Scope(null);

        readonly ParseTreeProperty<PigeonType> types = new ParseTreeProperty<PigeonType>();

        public override void EnterProgram([NotNull] PigeonParser.ProgramContext context)
        {
            scope = globalScope;
        }

        public override void EnterFunctionDecl([NotNull] PigeonParser.FunctionDeclContext context)
        {
            scope = new Scope(scope);

            var parameters = new List<Variable>();
            var parameterCount = context.functionParams().ID().Length;
            var returnType = PigeonType.FromName(context.TYPE().GetText());

            for (var i = 0; i < parameterCount; ++i)
            {
                var parameterType = PigeonType.FromName(context.functionParams().TYPE(i).GetText());
                var parameterName = context.functionParams().ID(i).GetText();
                var parameter = scope.DeclareVariable(parameterType, parameterName, false);
                parameters.Add(parameter);
            }

            globalScope.DeclareFunction(returnType, context.ID().GetText(), parameters.ToArray());
        }

        public override void ExitFunctionDecl([NotNull] PigeonParser.FunctionDeclContext context)
        {
            scope = scope.Parent;
        }

        public override void ExitFunctionCall([NotNull] PigeonParser.FunctionCallContext context)
        {
            var functionName = context.ID().GetText();
            if (!globalScope.TryLookupFunction(functionName, out var function))
            {
                ErrorBag.ReportUndeclaredFunction(context.GetTextSpan(), functionName);
                return;
            }
            var argumentCount = context.functionArgs().expr().Length;
            if (argumentCount != function.Parameters.Length)
            {
                ErrorBag.ReportInvalidNumberOfArguments(context.GetTextSpan(), functionName, argumentCount);
                return;
            }
            for (var i = 0; i < argumentCount; ++i)
            {
                var argument = context.functionArgs().expr(i);
                var argumentType = types.RemoveFrom(argument);
                if (argumentType != function.Parameters[i].Type)
                {
                    ErrorBag.ReportInvalidArgumentType(argument.GetTextSpan(), i, function.Parameters[i].Type);
                    return;
                }
            }
        }

        public override void ExitFunctionCallExpression([NotNull] PigeonParser.FunctionCallExpressionContext context)
        {
            var functionName = context.functionCall().ID().GetText();
            if (globalScope.TryLookupFunction(functionName, out var function))
            {
                ErrorBag.ReportUndeclaredFunction(context.GetTextSpan(), functionName);
                return;
            }
            types.Put(context, function.ReturnType);
        }

        public override void EnterStmtBlock([NotNull] PigeonParser.StmtBlockContext context)
        {
            scope = new Scope(scope);
        }

        public override void ExitStmtBlock([NotNull] PigeonParser.StmtBlockContext context)
        {
            scope = scope.Parent;
        }

        public override void EnterForStatement([NotNull] PigeonParser.ForStatementContext context)
        {
            scope = new Scope(scope);
            scope.DeclareVariable(PigeonType.Int, context.ID().GetText(), false);
        }

        public override void ExitForStatement([NotNull] PigeonParser.ForStatementContext context)
        {
            CheckExprType(context.expr(0), PigeonType.Int);
            CheckExprType(context.expr(1), PigeonType.Int);
            scope = scope.Parent;
        }

        public override void ExitWhileStatement([NotNull] PigeonParser.WhileStatementContext context)
        {
            CheckExprType(context.expr(), PigeonType.Bool);
        }

        public override void ExitDoWhileStatement([NotNull] PigeonParser.DoWhileStatementContext context)
        {
            CheckExprType(context.expr(), PigeonType.Bool);
        }

        public override void ExitIfStatement([NotNull] PigeonParser.IfStatementContext context)
        {
            CheckExprType(context.expr(), PigeonType.Bool);
        }

        public override void ExitVarDecl([NotNull] PigeonParser.VarDeclContext context)
        {
            var name = context.ID().GetText();
            var type = types.RemoveFrom(context.expr());
            if (scope.IsVariableDeclaredHere(name))
                ErrorBag.ReportVariableRedeclaration(context.GetTextSpan(), name);
            else
                scope.DeclareVariable(type, name, context.accessType.Text == "const");
        }

        public override void ExitVarAssign([NotNull] PigeonParser.VarAssignContext context)
        {
            var name = context.variable().ID().GetText();
            var valueType = types.RemoveFrom(context.expr());

            if (scope.TryLookupVariable(name, out var variable))
            {
                if (variable.ReadOnly)
                    ErrorBag.ReportRedefiningReadOnlyVariable(context.GetTextSpan(), name);
                if (!AssignmentOperator.IsAssignable(context.op.Text, variable.Type, valueType))
                    ErrorBag.ReportInvalidTypeAssignment(context.GetTextSpan(), name, variable.Type, valueType);
            }
            else
                ErrorBag.ReportUndeclaredVariable(context.variable().GetTextSpan(), name);
        }

        public override void ExitBreakStatement([NotNull] PigeonParser.BreakStatementContext context)
        {
            if (!IsInLoop(context))
                ErrorBag.ReportStatementNotInLoop(context.Start.GetTextSpan(), "break");
        }

        public override void ExitContinueStatement([NotNull] PigeonParser.ContinueStatementContext context)
        {
            if (!IsInLoop(context))
                ErrorBag.ReportStatementNotInLoop(context.Start.GetTextSpan(), "continue");
        }

        public override void ExitNumberLiteral([NotNull] PigeonParser.NumberLiteralContext context)
        {
            types.Put(context, context.GetText().Contains(".") ? PigeonType.Float : PigeonType.Int);
        }

        public override void ExitStringLiteral([NotNull] PigeonParser.StringLiteralContext context)
        {
            types.Put(context, PigeonType.String);
        }

        public override void ExitBoolLiteral([NotNull] PigeonParser.BoolLiteralContext context)
        {
            types.Put(context, PigeonType.Bool);
        }

        public override void ExitParenthesizedExpression([NotNull] PigeonParser.ParenthesizedExpressionContext context)
        {
            types.Put(context, types.RemoveFrom(context.expr()));
        }
        
        public override void ExitBinaryExpression([NotNull] PigeonParser.BinaryExpressionContext context)
        {
            var left = types.RemoveFrom(context.expr(0));
            var right = types.RemoveFrom(context.expr(1));
            if (!BinaryOperator.TryGetResType(context.op.Text, left, right, out var type))
                ErrorBag.ReportInvalidTypeBinaryOperator(context.op.GetTextSpan(), context.op.Text, left, right);
            types.Put(context, type);
        }

        public override void ExitUnaryExpression([NotNull] PigeonParser.UnaryExpressionContext context)
        {
            var operandType = types.RemoveFrom(context.expr());
            if (!UnaryOperator.TryGetResType(context.op.Text, operandType , out var type))
                ErrorBag.ReportInvalidTypeUnaryOperator(context.op.GetTextSpan(), context.op.Text, type);
            types.Put(context, type);
        }

        public override void ExitTernaryExpression([NotNull] PigeonParser.TernaryExpressionContext context)
        {
            CheckExprType(context.expr(0), PigeonType.Bool);
            
            var whenTrue = types.RemoveFrom(context.expr(1));
            var whenFalse = types.RemoveFrom(context.expr(2));
            if (!TernaryOperator.TryGetResType(whenTrue, whenFalse, out var type))
                ErrorBag.ReportInvalidTypeTernaryOperator(context.GetTextSpan(), whenTrue, whenFalse);
            
            types.Put(context, type);
        }

        public override void ExitVariableExpression([NotNull] PigeonParser.VariableExpressionContext context)
        {
            var name = context.variable().ID().GetText();
            if (scope.TryLookupVariable(name, out var variable))
                types.Put(context.variable(), variable.Type);
            else
                ErrorBag.ReportUndeclaredVariable(context.GetTextSpan(), name);
        }

        public override void ExitReturnStatement([NotNull] PigeonParser.ReturnStatementContext context)
        {
            var returnType = types.RemoveFrom(context.expr());
            
            RuleContext node = context;
            while (!(node is PigeonParser.FunctionDeclContext))
                node = node.Parent;
            
            var functionName = ((PigeonParser.FunctionDeclContext)node).ID().GetText();
            globalScope.TryLookupFunction(functionName, out var function);
            
            if (returnType != function.ReturnType)
            {
                ErrorBag.ReportUnexpectedType(context.expr().GetTextSpan(), returnType, function.ReturnType);
            }
        }

        private void CheckExprType(PigeonParser.ExprContext context, PigeonType expected)
        {
            var actual = types.RemoveFrom(context);
            if (actual != expected)
                ErrorBag.ReportUnexpectedType(context.GetTextSpan(), actual, expected);
        }

        private bool IsInLoop(RuleContext node)
        {
            while (node != null)
            {
                if (
                    node.Parent is PigeonParser.DoWhileStatementContext ||
                    node.Parent is PigeonParser.WhileStatementContext ||
                    node.Parent is PigeonParser.ForStatementContext
                ) return true;
                node = node.Parent;
            }
            return false;
        }
    }
}
