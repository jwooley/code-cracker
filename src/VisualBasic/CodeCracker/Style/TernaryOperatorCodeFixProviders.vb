﻿Imports System.Collections.Immutable
Imports System.Threading
Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CodeActions
Imports Microsoft.CodeAnalysis.CodeFixes
Imports Microsoft.CodeAnalysis.Formatting
Imports Microsoft.CodeAnalysis.VisualBasic
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Style

    Public MustInherit Class TernaryOperatorCodeFixProviderBase
        Inherits CodeFixProvider

        Protected Shared Function ExtractOperand(expression As AssignmentStatementSyntax, semanticModel As SemanticModel, type As ITypeSymbol, typeSyntax As TypeSyntax) As ExpressionSyntax
            Select Case expression.Kind
                Case SyntaxKind.AddAssignmentStatement
                    Return SyntaxFactory.AddExpression(expression.Left, expression.Right)
                Case SyntaxKind.SubtractAssignmentStatement
                    Return SyntaxFactory.SubtractExpression(expression.Left, expression.Right)
                Case SyntaxKind.ConcatenateAssignmentStatement
                    Return SyntaxFactory.ConcatenateExpression(expression.Left, expression.Right)
                Case SyntaxKind.DivideAssignmentStatement
                    Return SyntaxFactory.DivideExpression(expression.Left, expression.Right)
                Case SyntaxKind.ExponentiateAssignmentStatement
                    Return SyntaxFactory.ExponentiateExpression(expression.Left, expression.Right)
                Case SyntaxKind.IntegerDivideAssignmentStatement
                    Return SyntaxFactory.IntegerDivideExpression(expression.Left, expression.Right)
                Case SyntaxKind.LeftShiftAssignmentStatement
                    Return SyntaxFactory.LeftShiftExpression(expression.Left, expression.Right)
                Case SyntaxKind.MultiplyAssignmentStatement
                    Return SyntaxFactory.MultiplyExpression(expression.Left, expression.Right)
                Case SyntaxKind.RightShiftAssignmentStatement
                    Return SyntaxFactory.RightShiftExpression(expression.Left, expression.Right)
                Case Else
                    Return MakeTernaryOperand(expression.Right, semanticModel, type, typeSyntax)
            End Select
        End Function

        Protected Shared Function MakeTernaryOperand(expression As ExpressionSyntax, semanticModel As SemanticModel, type As ITypeSymbol, typeSyntax As TypeSyntax) As ExpressionSyntax
            If type?.OriginalDefinition.SpecialType = SpecialType.System_Nullable_T Then
                Dim constValue = semanticModel.GetConstantValue(expression)
                If constValue.HasValue AndAlso constValue.Value Is Nothing Then
                    Return SyntaxFactory.DirectCastExpression(expression.WithoutTrailingTrivia(), typeSyntax)
                End If
            End If

            Return expression
        End Function
    End Class

    <ExportCodeFixProvider(LanguageNames.VisualBasic, Name:=NameOf(TernaryOperatorWithReturnCodeFixProvider)), Composition.Shared>
    Public Class TernaryOperatorWithReturnCodeFixProvider
        Inherits TernaryOperatorCodeFixProviderBase

        Public Overrides Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim diagnostic = context.Diagnostics.First
            context.RegisterCodeFix(CodeAction.Create("Change to ternary operator", Function(c) MakeTernaryAsync(context.Document, diagnostic, c), NameOf(TernaryOperatorWithReturnCodeFixProvider)), diagnostic)
            Return Task.FromResult(0)
        End Function

        Public Overrides ReadOnly Property FixableDiagnosticIds As ImmutableArray(Of String) =
            ImmutableArray.Create(DiagnosticId.TernaryOperator_Return.ToDiagnosticId())

        Public Overrides Function GetFixAllProvider() As FixAllProvider
            Return WellKnownFixAllProviders.BatchFixer
        End Function

        Private Async Function MakeTernaryAsync(document As Document, diagnostic As Diagnostic, cancellationToken As CancellationToken) As Task(Of Document)
            Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)
            Dim span = diagnostic.Location.SourceSpan
            Dim ifBlock = root.FindToken(span.Start).Parent.FirstAncestorOrSelfOfType(Of MultiLineIfBlockSyntax)

            Dim ifReturn = TryCast(ifBlock.Statements.FirstOrDefault(), ReturnStatementSyntax)
            Dim elseReturn = TryCast(ifBlock.ElseBlock?.Statements.FirstOrDefault(), ReturnStatementSyntax)
            Dim semanticModel = Await document.GetSemanticModelAsync(cancellationToken)
            Dim type = semanticModel.GetTypeInfo(ifReturn.Expression).ConvertedType
            Dim typeSyntax = SyntaxFactory.IdentifierName(type.ToMinimalDisplayString(semanticModel, ifReturn.SpanStart))
            Dim trueExpression = MakeTernaryOperand(ifReturn.Expression, semanticModel, type, typeSyntax)
            Dim falseExpression = MakeTernaryOperand(elseReturn.Expression, semanticModel, type, typeSyntax)

            Dim leadingTrivia = ifBlock.GetLeadingTrivia()
            leadingTrivia = leadingTrivia.InsertRange(leadingTrivia.Count - 1, ifReturn.GetLeadingTrivia().Where(Function(trivia) trivia.IsKind(SyntaxKind.CommentTrivia)))
            leadingTrivia = leadingTrivia.InsertRange(leadingTrivia.Count - 1, elseReturn.GetLeadingTrivia().Where(Function(trivia) trivia.IsKind(SyntaxKind.CommentTrivia)))

            Dim trailingTrivia = ifBlock.GetTrailingTrivia.
                InsertRange(0, elseReturn.GetTrailingTrivia().Where(Function(trivia) Not trivia.IsKind(SyntaxKind.EndOfLineTrivia))).
                InsertRange(0, ifReturn.GetTrailingTrivia().Where(Function(trivia) Not trivia.IsKind(SyntaxKind.EndOfLineTrivia)))

            Dim ternary = SyntaxFactory.TernaryConditionalExpression(ifBlock.IfStatement.Condition.WithoutTrailingTrivia(),
                                                                     trueExpression.WithoutTrailingTrivia(),
                                                                     falseExpression.WithoutTrailingTrivia())

            Dim returnStatement = SyntaxFactory.ReturnStatement(ternary).
                WithLeadingTrivia(leadingTrivia).
                WithTrailingTrivia(trailingTrivia)

            Dim newRoot = root.ReplaceNode(ifBlock, returnStatement)
            Dim newDocument = document.WithSyntaxRoot(newRoot)

            Return newDocument
        End Function
    End Class

    <ExportCodeFixProvider(LanguageNames.VisualBasic, Name:=NameOf(TernaryOperatorWithAssignmentCodeFixProvider)), Composition.Shared>
    Public Class TernaryOperatorWithAssignmentCodeFixProvider
        Inherits TernaryOperatorCodeFixProviderBase

        Public Overrides Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim diagnostic = context.Diagnostics.First
            context.RegisterCodeFix(CodeAction.Create("Change to ternary operator", Function(c) MakeTernaryAsync(context.Document, diagnostic, c), NameOf(TernaryOperatorWithAssignmentCodeFixProvider)), diagnostic)
            Return Task.FromResult(0)
        End Function

        Public Overrides ReadOnly Property FixableDiagnosticIds() As ImmutableArray(Of String) =
            ImmutableArray.Create(DiagnosticId.TernaryOperator_Assignment.ToDiagnosticId())

        Public Overrides Function GetFixAllProvider() As FixAllProvider
            Return WellKnownFixAllProviders.BatchFixer
        End Function

        Private Async Function MakeTernaryAsync(document As Document, diagnostic As Diagnostic, cancellationToken As CancellationToken) As Task(Of Document)
            Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)
            Dim ifBlock = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.FirstAncestorOrSelf(Of MultiLineIfBlockSyntax)

            Dim ifAssign = TryCast(ifBlock.Statements.FirstOrDefault(), AssignmentStatementSyntax)
            Dim elseAssign = TryCast(ifBlock.ElseBlock?.Statements.FirstOrDefault(), AssignmentStatementSyntax)
            Dim semanticModel = Await document.GetSemanticModelAsync(cancellationToken)
            Dim type = semanticModel.GetTypeInfo(ifAssign.Left).ConvertedType
            Dim typeSyntax = SyntaxFactory.IdentifierName(type.ToMinimalDisplayString(semanticModel, ifAssign.SpanStart))

            Dim trueExpression = ExtractOperand(ifAssign, semanticModel, type, typeSyntax)
            Dim falseExpression = ExtractOperand(elseAssign, semanticModel, type, typeSyntax)

            Dim leadingTrivia = ifBlock.GetLeadingTrivia.
                AddRange(ifAssign.GetLeadingTrivia()).
                AddRange(trueExpression.GetLeadingTrivia()).
                AddRange(elseAssign.GetLeadingTrivia()).
                AddRange(falseExpression.GetLeadingTrivia())
            Dim trailingTrivia = ifBlock.GetTrailingTrivia.
                InsertRange(0, elseAssign.GetTrailingTrivia().Where(Function(trivia) Not trivia.IsKind(SyntaxKind.EndOfLineTrivia))).
                InsertRange(0, ifAssign.GetTrailingTrivia().Where(Function(trivia) Not trivia.IsKind(SyntaxKind.EndOfLineTrivia)))


            Dim ternary = SyntaxFactory.TernaryConditionalExpression(ifBlock.IfStatement.Condition.WithoutTrailingTrivia(),
                                                                     trueExpression.WithoutTrailingTrivia(),
                                                                     falseExpression.WithoutTrailingTrivia()).
                                                                     WithAdditionalAnnotations(Formatter.Annotation)

            Dim assignment = SyntaxFactory.SimpleAssignmentStatement(ifAssign.Left.WithLeadingTrivia(leadingTrivia), ternary).
                WithTrailingTrivia(trailingTrivia).
                WithAdditionalAnnotations(Formatter.Annotation)

            Dim newRoot = root.ReplaceNode(ifBlock, assignment)
            Dim newDocument = document.WithSyntaxRoot(newRoot)
            Return newDocument
        End Function
    End Class

    <ExportCodeFixProvider(LanguageNames.VisualBasic, Name:=NameOf(TernaryOperatorFromIifCodeFixProvider)), Composition.Shared>
    Public Class TernaryOperatorFromIifCodeFixProvider
        Inherits CodeFixProvider

        Public Overrides Function RegisterCodeFixesAsync(context As CodeFixContext) As Task
            Dim diagnostic = context.Diagnostics.First
            context.RegisterCodeFix(CodeAction.Create("Change IIF to If to short circuit evaulations", Function(c) MakeTernaryAsync(context.Document, diagnostic, c), NameOf(TernaryOperatorFromIifCodeFixProvider)), diagnostic)
            Return Task.FromResult(0)
        End Function

        Public Overrides ReadOnly Property FixableDiagnosticIds() As ImmutableArray(Of String) =
            ImmutableArray.Create(DiagnosticId.TernaryOperator_Iif.ToDiagnosticId())

        Public Overrides Function GetFixAllProvider() As FixAllProvider
            Return WellKnownFixAllProviders.BatchFixer
        End Function

        Private Async Function MakeTernaryAsync(document As Document, diagnostic As Diagnostic, cancellationToken As CancellationToken) As Task(Of Document)
            Dim root = Await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(False)
            Dim iifAssignment = root.FindToken(diagnostic.Location.SourceSpan.Start).Parent.FirstAncestorOrSelf(Of InvocationExpressionSyntax)

            Dim ternary = SyntaxFactory.TernaryConditionalExpression(
                iifAssignment.ArgumentList.Arguments(0).GetExpression(),
                iifAssignment.ArgumentList.Arguments(1).GetExpression(),
                iifAssignment.ArgumentList.Arguments(2).GetExpression()).
                WithLeadingTrivia(iifAssignment.GetLeadingTrivia()).
                WithTrailingTrivia(iifAssignment.GetTrailingTrivia()).
                WithAdditionalAnnotations(Formatter.Annotation)

            Dim newRoot = root.ReplaceNode(iifAssignment, ternary)
            Dim newDocument = document.WithSyntaxRoot(newRoot)
            Return newDocument
        End Function
    End Class
End Namespace