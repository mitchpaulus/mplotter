//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     ANTLR Version: 4.13.1
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

// Generated from Mconfig.g4 by ANTLR 4.13.1

// Unreachable code detected
#pragma warning disable 0162
// The variable '...' is assigned but its value is never used
#pragma warning disable 0219
// Missing XML comment for publicly visible type or member '...'
#pragma warning disable 1591
// Ambiguous reference in cref attribute
#pragma warning disable 419


using Antlr4.Runtime.Misc;
using IErrorNode = Antlr4.Runtime.Tree.IErrorNode;
using ITerminalNode = Antlr4.Runtime.Tree.ITerminalNode;
using IToken = Antlr4.Runtime.IToken;
using ParserRuleContext = Antlr4.Runtime.ParserRuleContext;

/// <summary>
/// This class provides an empty implementation of <see cref="IMconfigListener"/>,
/// which can be extended to create a listener which only needs to handle a subset
/// of the available methods.
/// </summary>
[System.CodeDom.Compiler.GeneratedCode("ANTLR", "4.13.1")]
[System.Diagnostics.DebuggerNonUserCode]
[System.CLSCompliant(false)]
public partial class MconfigBaseListener : IMconfigListener {
	/// <summary>
	/// Enter a parse tree produced by <see cref="MconfigParser.file"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterFile([NotNull] MconfigParser.FileContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="MconfigParser.file"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitFile([NotNull] MconfigParser.FileContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="MconfigParser.record"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRecord([NotNull] MconfigParser.RecordContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="MconfigParser.record"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRecord([NotNull] MconfigParser.RecordContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>TypeSelectorInfo</c>
	/// labeled alternative in <see cref="MconfigParser.sourceSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterTypeSelectorInfo([NotNull] MconfigParser.TypeSelectorInfoContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>TypeSelectorInfo</c>
	/// labeled alternative in <see cref="MconfigParser.sourceSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitTypeSelectorInfo([NotNull] MconfigParser.TypeSelectorInfoContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>AbsPathSelectorInfo</c>
	/// labeled alternative in <see cref="MconfigParser.sourceSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAbsPathSelectorInfo([NotNull] MconfigParser.AbsPathSelectorInfoContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>AbsPathSelectorInfo</c>
	/// labeled alternative in <see cref="MconfigParser.sourceSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAbsPathSelectorInfo([NotNull] MconfigParser.AbsPathSelectorInfoContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="MconfigParser.typeSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterTypeSelector([NotNull] MconfigParser.TypeSelectorContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="MconfigParser.typeSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitTypeSelector([NotNull] MconfigParser.TypeSelectorContext context) { }
	/// <summary>
	/// Enter a parse tree produced by <see cref="MconfigParser.absPathSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterAbsPathSelector([NotNull] MconfigParser.AbsPathSelectorContext context) { }
	/// <summary>
	/// Exit a parse tree produced by <see cref="MconfigParser.absPathSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitAbsPathSelector([NotNull] MconfigParser.AbsPathSelectorContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>RegexTrendSelector</c>
	/// labeled alternative in <see cref="MconfigParser.trendSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRegexTrendSelector([NotNull] MconfigParser.RegexTrendSelectorContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>RegexTrendSelector</c>
	/// labeled alternative in <see cref="MconfigParser.trendSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRegexTrendSelector([NotNull] MconfigParser.RegexTrendSelectorContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>NameTrendSelector</c>
	/// labeled alternative in <see cref="MconfigParser.trendSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterNameTrendSelector([NotNull] MconfigParser.NameTrendSelectorContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>NameTrendSelector</c>
	/// labeled alternative in <see cref="MconfigParser.trendSelector"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitNameTrendSelector([NotNull] MconfigParser.NameTrendSelectorContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>UnitTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterUnitTransform([NotNull] MconfigParser.UnitTransformContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>UnitTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitUnitTransform([NotNull] MconfigParser.UnitTransformContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ConvertTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterConvertTransform([NotNull] MconfigParser.ConvertTransformContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ConvertTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitConvertTransform([NotNull] MconfigParser.ConvertTransformContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>RenameTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterRenameTransform([NotNull] MconfigParser.RenameTransformContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>RenameTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitRenameTransform([NotNull] MconfigParser.RenameTransformContext context) { }
	/// <summary>
	/// Enter a parse tree produced by the <c>ReplaceTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void EnterReplaceTransform([NotNull] MconfigParser.ReplaceTransformContext context) { }
	/// <summary>
	/// Exit a parse tree produced by the <c>ReplaceTransform</c>
	/// labeled alternative in <see cref="MconfigParser.transform"/>.
	/// <para>The default implementation does nothing.</para>
	/// </summary>
	/// <param name="context">The parse tree.</param>
	public virtual void ExitReplaceTransform([NotNull] MconfigParser.ReplaceTransformContext context) { }

	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void EnterEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void ExitEveryRule([NotNull] ParserRuleContext context) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitTerminal([NotNull] ITerminalNode node) { }
	/// <inheritdoc/>
	/// <remarks>The default implementation does nothing.</remarks>
	public virtual void VisitErrorNode([NotNull] IErrorNode node) { }
}
