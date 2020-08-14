﻿enum TokenType
{ 
    If,
    For,
    Do,
    While,
    Break,
    Continue,
    Return,

    Comment,
    Identifier,
    BlockComment,

    IntType,
    FloatType,
    BoolType,
    StringType,
    
    IntConst,
    FloatConst,
    BoolConst,
    StringConst,

    LPar,
    RPar,
    LBrace,
    RBrace,
    Add,
    AddEq,
    Inc,
    Sub,
    SubEq,
    Dec,
    Mul,
    MulEq,
    Div,
    DivEq,
    Mod,
    ModEq,
    Not,
    And,
    Or,
    Lt,
    Gt,
    Leq,
    Geq,
    Eq,
    Neq,
    Quest,
    Colon,
    Semicolon,
}