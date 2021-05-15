﻿using Antlr4.Runtime.Misc;
using Kostic017.Pigeon.Symbols;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Kostic017.Pigeon
{
    class BreakLoopException : Exception { }
    
    class FuncReturnValueException : Exception
    {
        internal object Value { get; }

        internal FuncReturnValueException(object value)
        {
            Value = value;
        }
    }

    class Evaluator : PigeonBaseVisitor<object>
    {
        private readonly SemanticAnalyser analyser;

        internal Evaluator(SemanticAnalyser analyser)
        {
            this.analyser = analyser;
        }

        public override object VisitProgram([NotNull] PigeonParser.ProgramContext context)
        {
            foreach (var stmt in context.stmt())
                VisitStmt(stmt);
            return null;
        }

        public override object VisitParenthesizedExpression([NotNull] PigeonParser.ParenthesizedExpressionContext context)
        {
            return VisitExpr(context.expr());
        }

        public override object VisitBoolLiteral([NotNull] PigeonParser.BoolLiteralContext context)
        {
            return bool.Parse(context.BOOL().GetText());
        }

        public override object VisitStringLiteral([NotNull] PigeonParser.StringLiteralContext context)
        {
            return context.STRING().GetText().Trim('"');
        }

        public override object VisitNumberLiteral([NotNull] PigeonParser.NumberLiteralContext context)
        {
            if (analyser.Types.Get(context) == PigeonType.Int)
                return int.Parse(context.NUMBER().GetText());
            return float.Parse(context.NUMBER().GetText(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        public override object VisitUnaryExpression([NotNull] PigeonParser.UnaryExpressionContext context)
        {
            var operand = VisitExpr(context.expr());
            var resType = analyser.Types.Get(context);
            switch (context.op.Text)
            {
                case "+":
                    if (resType == PigeonType.Int)
                        return (int)operand;
                    return (float)operand;
                case "-":
                    if (resType == PigeonType.Int)
                        return -(int)operand;
                    return -(float)operand;
                case "!":
                    return !(bool)operand;
                default:
                    throw new InternalErrorException($"Unsupported unary operator {context.op.Text}");
            }
        }

        public override object VisitBinaryExpression([NotNull] PigeonParser.BinaryExpressionContext context)
        {
            var left = VisitExpr(context.expr(0));
            var right = VisitExpr(context.expr(1));
            var resType = analyser.Types.Get(context);

            var areBothInt =
                analyser.Types.Get(context.expr(0)) == PigeonType.Int &&
                analyser.Types.Get(context.expr(1)) == PigeonType.Int;
            
            switch (context.op.Text)
            {
                case "==":
                    return left.Equals(right);

                case "!=":
                    return !left.Equals(right);

                case "&&":
                    return (bool)left && (bool)right;

                case "||":
                    return (bool)left || (bool)right;

                case "<":
                    if (areBothInt)
                        return (int)left < (int)right;
                    return (float)left < (float)right;

                case ">":
                    if (areBothInt)
                        return (int)left > (int)right;
                    return (float)left > (float)right;

                case "<=":
                    if (areBothInt)
                        return (int)left <= (int)right;
                    return (float)left <= (float)right;

                case ">=":
                    if (areBothInt)
                        return (int)left >= (int)right;
                    return (float)left >= (float)right;

                case "+":
                    if (resType == PigeonType.Int)
                        return (int)left + (int)right;
                    else if (resType == PigeonType.Float)
                        return (float)left + (float)right;
                    else
                        return left.ToString() + right.ToString();

                case "-":
                    if (resType == PigeonType.Int)
                        return (int)left - (int)right;
                    return (float)left - (float)right;

                case "*":
                    if (resType == PigeonType.Int)
                        return (int)left * (int)right;
                    return (float)left * (float)right;

                case "/":
                    if (resType == PigeonType.Int)
                        return (int)left / (int)right;
                    return (float)left / (float)right;

                case "%":
                    return (int)left % (int)right;

                default:
                    throw new InternalErrorException($"Unsupported binary operator {context.op.Text}");
            }
        }

        public override object VisitFunctionCallExpression([NotNull] PigeonParser.FunctionCallExpressionContext context)
        {
            return VisitFunctionCall(context.functionCall());
        }

        public override object VisitTernaryExpression([NotNull] PigeonParser.TernaryExpressionContext context)
        {
            var condition = VisitExpr(context.expr(0));
            var whenTrue = VisitExpr(context.expr(1));
            var whenFalse = VisitExpr(context.expr(2));
            return (bool) condition ? whenTrue : whenFalse;
        }

        public override object VisitVariableExpression([NotNull] PigeonParser.VariableExpressionContext context)
        {
            return analyser.Scopes.Get(context).Evaluate(context.ID().GetText());
        }

        public override object VisitIfStatement([NotNull] PigeonParser.IfStatementContext context)
        {
            if ((bool) VisitExpr(context.expr()))
                VisitStmtBlock(context.stmtBlock(0));
            else if (context.stmtBlock(1) != null)
                VisitStmtBlock(context.stmtBlock(1));
            return null;
        }

        public override object VisitDoWhileStatement([NotNull] PigeonParser.DoWhileStatementContext context)
        {
            do
                try
                {
                    VisitStmtBlock(context.stmtBlock());
                }
                catch (BreakLoopException)
                {
                    return null;
                }
            while ((bool) VisitExpr(context.expr()));
            return null;
        }

        public override object VisitWhileStatement([NotNull] PigeonParser.WhileStatementContext context)
        {
            while ((bool) VisitExpr(context.expr()))
                try
                {
                    VisitStmtBlock(context.stmtBlock());
                }
                catch (BreakLoopException)
                {
                    return null;
                }
            return null;
        }

        public override object VisitForStatement([NotNull] PigeonParser.ForStatementContext context)
        {
            var bodyScope = analyser.Scopes.Get(context.stmtBlock());
            var startValue = (int) VisitExpr(context.expr(0));
            var targetValue = (int) VisitExpr(context.expr(1));
            var isIncrementing = context.dir.Text == "to";

            var i = startValue;
            bodyScope.Assign(context.ID().GetText(), i);
            while (isIncrementing ? i <= targetValue : i >= targetValue)
            {
                try
                {
                    VisitStmtBlock(context.stmtBlock());
                }
                catch (BreakLoopException)
                {
                    return null;
                }
                i += isIncrementing ? 1 : -1;
                bodyScope.Assign(context.ID().GetText(), i);
            }

            return null;
        }

        public override object VisitFunctionCallStatement([NotNull] PigeonParser.FunctionCallStatementContext context)
        {
            return VisitFunctionCall(context.functionCall());
        }

        public override object VisitStmtBlock([NotNull] PigeonParser.StmtBlockContext context)
        {
            foreach (var statement in context.stmt())
            {
                var r = VisitStmt(statement);
                if (statement is PigeonParser.ContinueStatementContext)
                    return null;
                if (statement is PigeonParser.BreakStatementContext)
                    throw new BreakLoopException();
                if (statement is PigeonParser.ReturnStatementContext)
                    throw new FuncReturnValueException(r);
            }
            return null;
        }

        public override object VisitReturnStatement([NotNull] PigeonParser.ReturnStatementContext context)
        {
            return VisitExpr(context.expr());
        }

        public override object VisitVariableAssignmentStatement([NotNull] PigeonParser.VariableAssignmentStatementContext context)
        {
            return VisitVarAssign(context.varAssign());
        }

        public override object VisitVarAssign([NotNull] PigeonParser.VarAssignContext context)
        {
            var scope = analyser.Scopes.Get(context);
            var name = context.ID().GetText();
            var type = analyser.Types.Get(context.expr());
            var value = VisitExpr(context.expr());
            var currentValue = scope.Evaluate(name);

            switch (context.op.Text)
            {
                case "=":
                    scope.Assign(name, value);
                    break;

                case "+=":
                    if (type == PigeonType.Int)
                        scope.Assign(name, (int) currentValue + (int) value);
                    else if (type == PigeonType.Float)
                        scope.Assign(name, (float) currentValue + (float) value);
                    else
                        scope.Assign(name, (string) currentValue + (string) value);
                    break;

                case "-=":
                    if (type == PigeonType.Int)
                        scope.Assign(name, (int) currentValue - (int) value);
                    else if (type == PigeonType.Float)
                        scope.Assign(name, (float) currentValue - (float) value);
                    break;

                case "*=":
                    if (type == PigeonType.Int)
                        scope.Assign(name, (int) currentValue * (int) value);
                    else if (type == PigeonType.Float)
                        scope.Assign(name, (float) currentValue * (float) value);
                    break;

                case "/=":
                    if (type == PigeonType.Int)
                        scope.Assign(name, (int) currentValue / (int) value);
                    else if (type == PigeonType.Float)
                        scope.Assign(name, (float) currentValue / (float) value);
                    break;

                case "%=":
                    scope.Assign(name, (int) currentValue / (int) value);
                    break;
            }

            return null;
        }

        public override object VisitFunctionCall([NotNull] PigeonParser.FunctionCallContext context)
        {
            var callSiteScope = analyser.Scopes.Get(context);
            analyser.GlobalScope.TryGetFunction(context.ID().GetText(), out var function);

            var argValues = new List<object>();
            foreach (var arg in context.functionArgs().expr())
                argValues.Add(VisitExpr(arg));

            if (function.FuncBody is FuncPointer fp)
                return fp(argValues.ToArray());

            var funcBody = (PigeonParser.StmtBlockContext)function.FuncBody;
            var funcScope = analyser.Scopes.Get(funcBody);
            funcScope.Restart();
            
            for (var i = 0; i < argValues.Count; ++i)
                funcScope.Assign(function.Parameters[i].Name, argValues[i]);

            try
            {
                return VisitStmtBlock(funcBody);
            }
            catch (FuncReturnValueException e)
            {
                return e.Value;
            }
        }

        public override object VisitStmt([NotNull] PigeonParser.StmtContext context)
        {
            if (context is PigeonParser.IfStatementContext ctxi)
                return VisitIfStatement(ctxi);
            if (context is PigeonParser.DoWhileStatementContext ctxd)
                return VisitDoWhileStatement(ctxd);
            if (context is PigeonParser.WhileStatementContext ctxw)
                return VisitWhileStatement(ctxw);
            if (context is PigeonParser.ForStatementContext ctxf)
                return VisitForStatement(ctxf);
            if (context is PigeonParser.FunctionCallStatementContext ctxfu)
                return VisitFunctionCallStatement(ctxfu);
            if (context is PigeonParser.ReturnStatementContext ctxr)
                return VisitReturnStatement(ctxr);
            if (context is PigeonParser.VariableAssignmentStatementContext ctxv)
                return VisitVariableAssignmentStatement(ctxv);
            throw new InternalErrorException($"Unsupported statement type {context.GetType()}");
        }

        public override object VisitExpr([NotNull] PigeonParser.ExprContext context)
        {
            if (context is PigeonParser.ParenthesizedExpressionContext ctxp)
                return VisitParenthesizedExpression(ctxp);
            if (context is PigeonParser.BoolLiteralContext ctxb)
                return VisitBoolLiteral(ctxb);
            if (context is PigeonParser.StringLiteralContext ctxs)
                return VisitStringLiteral(ctxs);
            if (context is PigeonParser.NumberLiteralContext ctxn)
                return VisitNumberLiteral(ctxn);
            if (context is PigeonParser.UnaryExpressionContext ctxu)
                return VisitUnaryExpression(ctxu);
            if (context is PigeonParser.BinaryExpressionContext ctxbi)
                return VisitBinaryExpression(ctxbi);
            if (context is PigeonParser.FunctionCallExpressionContext ctxf)
                return VisitFunctionCallExpression(ctxf);
            if (context is PigeonParser.TernaryExpressionContext ctxt)
                return VisitTernaryExpression(ctxt);
            if (context is PigeonParser.VariableExpressionContext ctxv)
                return VisitVariableExpression(ctxv);
            throw new InternalErrorException($"Unsupported expression type {context.GetType()}");
        }
        
    }
}
