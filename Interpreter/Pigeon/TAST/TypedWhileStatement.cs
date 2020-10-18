﻿using Kostic017.Pigeon.TAST;

namespace Kostic017.Pigeon
{
    class TypedWhileStatement : TypedStatement
    {
        internal TypedExpression Condition { get; }
        internal TypedStatementBlock Body { get; }

        internal TypedWhileStatement(TypedExpression condition, TypedStatementBlock body)
        {
            Condition = condition;
            Body = body;
        }

        internal override NodeKind Kind => NodeKind.WhileStatement;
    }
}