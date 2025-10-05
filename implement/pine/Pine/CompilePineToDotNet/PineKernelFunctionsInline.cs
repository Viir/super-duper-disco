using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pine.Core;
using Pine.Core.DotNet;
using Pine.Core.PopularEncodings;
using System;
using System.Collections.Generic;

namespace Pine.CompilePineToDotNet;

using CoreSyntaxFactory = Core.DotNet.PineCSharpSyntaxFactory;

public class PineKernelFunctionsInline
{
    public static Result<string, CompiledExpression>? TryInlineKernelFunction_Equal(
        Expression argumentExpression,
        ExpressionCompilationEnvironment compilationEnv)
    {
        return
            argumentExpression switch
            {
                Expression.List argumentList =>
                argumentList.Items switch
                {
                    [var firstArgument, var secondArgument] =>
                    TryInlineKernelFunction_Equal_two_args(firstArgument, secondArgument, compilationEnv),

                    _ =>
                    null
                },

                _ =>
                null
            };
    }

    public static Result<string, CompiledExpression>? TryInlineKernelFunction_Equal_two_args(
        Expression firstArgument,
        Expression secondArgument,
        ExpressionCompilationEnvironment compilationEnv)
    {
        if (IsKernelAppTakingZeroFrom(firstArgument) is Expression firstArgumentTakenZero)
        {
            if (secondArgument == Expression.LiteralInstance(PineValue.EmptyBlob))
            {
                return
                    CompileToCSharp.CompileToCSharpExpression(
                        firstArgumentTakenZero,
                        compilationEnv,
                        createLetBindingsForCse: false)
                    .Map(firstArgumentTakenZeroCompiled =>
                    firstArgumentTakenZeroCompiled
                    .Map(
                        environment: compilationEnv,
                        firstArgumentTakenZeroCompiledOk =>
                        CoreSyntaxFactory.PineValueFromBoolExpression(
                            PineCSharpSyntaxFactory.BuildCSharpExpressionToCheckIsBlob(firstArgumentTakenZeroCompiledOk),
                            compilationEnv.FunctionEnvironment.DeclarationSyntaxContext)));
            }

            if (secondArgument == Expression.LiteralInstance(PineValue.EmptyList))
            {
                return
                    CompileToCSharp.CompileToCSharpExpression(
                        firstArgumentTakenZero,
                        compilationEnv,
                        createLetBindingsForCse: false)
                    .Map(firstArgumentTakenZeroCompiled =>
                    firstArgumentTakenZeroCompiled
                    .Map(
                        environment: compilationEnv,
                        firstArgumentTakenZeroCompiledOk =>
                        CoreSyntaxFactory.PineValueFromBoolExpression(
                            PineCSharpSyntaxFactory.BuildCSharpExpressionToCheckIsList(firstArgumentTakenZeroCompiledOk),
                            compilationEnv.FunctionEnvironment.DeclarationSyntaxContext)));
            }
        }

        return
            CompileToCSharp.CompileToCSharpExpression(
                firstArgument,
                compilationEnv,
                createLetBindingsForCse: false)
            .AndThen(
                compileFirstArgOk =>
                CompileToCSharp.CompileToCSharpExpression(
                    secondArgument,
                    compilationEnv,
                    createLetBindingsForCse: false)
                .Map(compileSecondArgOk =>
                compileFirstArgOk
                .MapOrAndThen(
                    compilationEnv,
                    firstArgCs =>
                    compileSecondArgOk
                    .Map(
                        compilationEnv,
                        secondArgCs =>
                        CoreSyntaxFactory.PineValueFromBoolExpression(
                            SyntaxFactory.BinaryExpression(
                                SyntaxKind.EqualsExpression,
                                firstArgCs,
                                secondArgCs),
                            compilationEnv.FunctionEnvironment.DeclarationSyntaxContext)))));
    }

    public static Expression? IsKernelAppTakingZeroFrom(Expression expression)
    {
        if (expression is not Expression.KernelApplication kernelApp)
            return null;

        if (kernelApp.Function is not nameof(KernelFunction.take))
            return null;

        if (kernelApp.Input is not Expression.List takeArgumentList)
            return null;

        if (takeArgumentList.Items.Count is not 2)
            return null;

        if (takeArgumentList.Items[0] != Expression.LiteralInstance(IntegerEncoding.EncodeSignedInteger(0)))
            return null;

        return takeArgumentList.Items[1];
    }

    public static Result<string, CompiledExpression>? TryInlineKernelFunction_Length(
        Expression argumentExpression,
        ExpressionCompilationEnvironment compilationEnv)
    {
        return
            CompileToCSharp.CompileToCSharpExpression(
                argumentExpression,
                compilationEnv,
                createLetBindingsForCse: false)
            .Map(compileOk =>
            compileOk.Map(
                compilationEnv,
                argumentExpression =>
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(nameof(IntegerEncoding)),
                        SyntaxFactory.IdentifierName(nameof(IntegerEncoding.EncodeSignedInteger))))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(
                                LengthFromPineValueExpressionAsInt(argumentExpression)))))));
    }

    public static ExpressionSyntax LengthFromPineValueExpressionAsInt(ExpressionSyntax argumentExpression) =>
        SyntaxFactory.SwitchExpression(argumentExpression)
        .WithArms(
            SyntaxFactory.SeparatedList<SwitchExpressionArmSyntax>(
                new SyntaxNodeOrToken[]{
                    SyntaxFactory.SwitchExpressionArm(
                        SyntaxFactory.DeclarationPattern(
                            SyntaxFactory.QualifiedName(
                                SyntaxFactory.IdentifierName("PineValue"),
                                SyntaxFactory.IdentifierName("BlobValue")),
                            SyntaxFactory.SingleVariableDesignation(
                                SyntaxFactory.Identifier("blobValue"))),
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("blobValue"),
                                SyntaxFactory.IdentifierName("Bytes")),
                            SyntaxFactory.IdentifierName("Length"))),
                    SyntaxFactory.Token(SyntaxKind.CommaToken),
                    SyntaxFactory.SwitchExpressionArm(
                        SyntaxFactory.DeclarationPattern(
                            SyntaxFactory.QualifiedName(
                                SyntaxFactory.IdentifierName("PineValue"),
                                SyntaxFactory.IdentifierName("ListValue")),
                            SyntaxFactory.SingleVariableDesignation(
                                SyntaxFactory.Identifier("listValue"))),
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("listValue"),
                                SyntaxFactory.IdentifierName("Elements")),
                            SyntaxFactory.IdentifierName("Count"))),
                    SyntaxFactory.Token(SyntaxKind.CommaToken),
                    SwitchExpressionArmDefaultToNotImplementedException
                }));

    public static Result<string, CompiledExpression>? TryInlineKernelFunction_ListHead(
        Expression argumentExpression,
        ExpressionCompilationEnvironment compilationEnv)
    {
        return
            CompileToCSharp.CompileToCSharpExpression(
                argumentExpression,
                compilationEnv,
                createLetBindingsForCse: false)
            .Map(compileOk =>
            compileOk.Map(
                compilationEnv,
                argumentExpression =>
                SyntaxFactory.SwitchExpression(argumentExpression)
                .WithArms(
                    SyntaxFactory.SeparatedList<SwitchExpressionArmSyntax>(
                        new SyntaxNodeOrToken[]
                        {
                            SyntaxFactory.SwitchExpressionArm(
                                SyntaxFactory.DeclarationPattern(
                                    SyntaxFactory.QualifiedName(
                                        SyntaxFactory.IdentifierName("PineValue"),
                                        SyntaxFactory.IdentifierName("ListValue")),
                                    SyntaxFactory.SingleVariableDesignation(
                                        SyntaxFactory.Identifier("listValue"))),
                                SyntaxFactory.SwitchExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("listValue"),
                                        SyntaxFactory.IdentifierName("Elements")))
                                .WithArms(
                                    SyntaxFactory.SeparatedList<SwitchExpressionArmSyntax>(
                                        new SyntaxNodeOrToken[] {
                                            SyntaxFactory.SwitchExpressionArm(
                                                SyntaxFactory.ListPattern(
                                                    SyntaxFactory.SeparatedList<PatternSyntax>(
                                                        new SyntaxNodeOrToken[] {
                                                            SyntaxFactory.VarPattern(
                                                                SyntaxFactory.SingleVariableDesignation(
                                                                    SyntaxFactory.Identifier("head"))),
                                                            SyntaxFactory.Token(SyntaxKind.CommaToken),
                                                            SyntaxFactory.SlicePattern()
                                                        })),
                                                SyntaxFactory.IdentifierName("head")),
                                            SyntaxFactory.Token(SyntaxKind.CommaToken),
                                            SwitchExpressionArmDefaultToPineValueEmptyList(
                                                compilationEnv.FunctionEnvironment.DeclarationSyntaxContext)
                                        }))),
                            SyntaxFactory.Token(SyntaxKind.CommaToken),
                            SwitchExpressionArmDefaultToPineValueEmptyList(
                                compilationEnv.FunctionEnvironment.DeclarationSyntaxContext)
                        }
                        ))));
    }

    public static Result<string, CompiledExpression>? TryInlineKernelFunction_Skip(
        Expression argumentExpression,
        ExpressionCompilationEnvironment compilationEnv)
    {
        var staticallyKnownArgumentsList =
            CompileToCSharp.ParseKernelApplicationInputAsList(argumentExpression, compilationEnv)
            ?.Unpack(fromErr: err =>
            {
                Console.WriteLine("Failed to parse argument list: " + err);
                return null;
            },
            fromOk: ok => ok);

        return
            staticallyKnownArgumentsList switch
            {
                [var firstArgument, var secondArgument] =>
                firstArgument.AsLiteralInt64 switch
                {
                    { } count
                    when count < int.MaxValue =>
                    secondArgument.ArgumentSyntaxFromParameterType.GetValueOrDefault(
                        PineKernelFunctions.KernelFunctionParameterType.Generic) switch
                    {
                        { } secondArgumentCompiled =>
                        Result<string, CompiledExpression>.ok(
                            secondArgumentCompiled
                            .Map(
                                compilationEnv,
                                secondArgCs =>
                                Kernel_Skip(
                                    (int)Math.Max(0, count),
                                    secondArgCs,
                                    compilationEnv.FunctionEnvironment.DeclarationSyntaxContext))),

                        _ =>
                        null
                    },

                    _ =>
                    null
                },

                _ =>
                null
            };
    }

    public static ExpressionSyntax Kernel_Skip(
        int count,
        ExpressionSyntax originalValue,
        DeclarationSyntaxContext declarationSyntaxContext)
    {
        var countExpression = CoreSyntaxFactory.ExpressionSyntaxForIntegerLiteral(count);

        return
            SyntaxFactory.SwitchExpression(originalValue)
            .WithArms(
                SyntaxFactory.SeparatedList<SwitchExpressionArmSyntax>(
                    new SyntaxNodeOrToken[]{
                        SyntaxFactory.SwitchExpressionArm(
                            SyntaxFactory.DeclarationPattern(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("PineValue"),
                                    SyntaxFactory.IdentifierName("ListValue")),
                                SyntaxFactory.SingleVariableDesignation(
                                    SyntaxFactory.Identifier("listValue"))),
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("PineValue"),
                                    SyntaxFactory.IdentifierName("ListValue")))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.CollectionExpression(
                                                SyntaxFactory.SingletonSeparatedList<CollectionElementSyntax>(
                                                    SyntaxFactory.SpreadElement(
                                                        SyntaxFactory.InvocationExpression(
                                                            SyntaxFactory.MemberAccessExpression(
                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                SyntaxFactory.MemberAccessExpression(
                                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                                    SyntaxFactory.IdentifierName("listValue"),
                                                                    SyntaxFactory.IdentifierName("Elements")),
                                                                SyntaxFactory.IdentifierName("Skip")))
                                                        .WithArgumentList(
                                                            SyntaxFactory.ArgumentList(
                                                                SyntaxFactory.SingletonSeparatedList(
                                                                    SyntaxFactory.Argument(
                                                                        countExpression)))))))))))),
                        SyntaxFactory.Token(SyntaxKind.CommaToken),
                        SyntaxFactory.SwitchExpressionArm(
                            SyntaxFactory.DeclarationPattern(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("PineValue"),
                                    SyntaxFactory.IdentifierName("BlobValue")),
                                SyntaxFactory.SingleVariableDesignation(
                                    SyntaxFactory.Identifier("blobValue"))),
                            SyntaxFactory.ObjectCreationExpression(
                                SyntaxFactory.QualifiedName(
                                    SyntaxFactory.IdentifierName("PineValue"),
                                    SyntaxFactory.IdentifierName("BlobValue")))
                            .WithArgumentList(
                                SyntaxFactory.ArgumentList(
                                    SyntaxFactory.SingletonSeparatedList(
                                        SyntaxFactory.Argument(
                                            SyntaxFactory.ElementAccessExpression(
                                                SyntaxFactory.MemberAccessExpression(
                                                    SyntaxKind.SimpleMemberAccessExpression,
                                                    SyntaxFactory.IdentifierName("blobValue"),
                                                    SyntaxFactory.IdentifierName("Bytes")))
                                            .WithArgumentList(
                                                SyntaxFactory.BracketedArgumentList(
                                                    SyntaxFactory.SingletonSeparatedList(
                                                        SyntaxFactory.Argument(
                                                            SyntaxFactory.RangeExpression()
                                                            .WithLeftOperand(
                                                                countExpression)))))))))),
                        SyntaxFactory.Token(SyntaxKind.CommaToken),
                        SwitchExpressionArmDefaultToPineValueEmptyList(
                            declarationSyntaxContext)
                    }));
    }

    public static readonly SwitchExpressionArmSyntax SwitchExpressionArmDefaultToNotImplementedException =
        SyntaxFactory.SwitchExpressionArm(
            SyntaxFactory.DiscardPattern(),
            SyntaxFactory.ThrowExpression(
                SyntaxFactory.ObjectCreationExpression(
                    SyntaxFactory.IdentifierName(nameof(NotImplementedException)))
                .WithArgumentList(
                    SyntaxFactory.ArgumentList())));


    public static SwitchExpressionArmSyntax SwitchExpressionArmDefaultToPineValueEmptyList(
        DeclarationSyntaxContext declarationSyntaxContext)
    {
        return
            SyntaxFactory.SwitchExpressionArm(
                SyntaxFactory.DiscardPattern(),
                CoreSyntaxFactory.PineValueEmptyListSyntax(declarationSyntaxContext));
    }

}
