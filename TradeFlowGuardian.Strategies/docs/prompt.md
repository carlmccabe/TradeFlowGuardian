# Prompt Template for Generating Comprehensive Code Documentation

Use this prompt when you want to generate verbose, production-quality documentation for any class, method, or module in your codebase:

---

## **Documentation Generation Prompt**

```
Please add comprehensive XML documentation comments to [CLASS/METHOD/FILE NAME] following these requirements:

**Documentation Style Reference:**
Use the same verbose, educational style as the signal implementations in TradeFlowGuardian.Strategies (BreakoutSignal, EMACrossoverSignal, SMACrossoverSignal, MACDSignal, RSIReversionSignal, InsideBarBreakoutSignal).

**Required Components:**

1. **Class-Level Summary**
   - Single-sentence description of purpose
   - Target audience (developers, traders, or both)

2. **Detailed Remarks Section**
   - What problem does this solve?
   - How does it work? (algorithm/logic explanation)
   - When should it be used?
   - Key characteristics and behavior
   - Mathematical formulas (if applicable) with LaTeX or plain text
   - Performance characteristics
   - Thread-safety notes (if relevant)

3. **Usage Examples**
   - At least 3 concrete code examples showing:
     * Basic usage (copy-paste ready)
     * Advanced usage
     * Common pitfalls and how to avoid them
   - Include realistic parameter values and context

4. **Parameter Documentation**
   - Each parameter must have:
     * Brief description
     * Valid range/constraints
     * Default value (if applicable)
     * Performance/behavior implications
     * Example values for different use cases
   - Group related parameters with explanatory text

5. **Comparison Sections** (where applicable)
   - "X vs Y" comparison tables
   - "When to use X over Y" decision tree
   - Trade-offs explained

6. **Tuning Guidelines**
   - Parameter recommendations for different scenarios
   - Environment-specific advice (dev/test/production)
   - Scaling considerations

7. **Limitations and Considerations**
   - Known edge cases
   - Performance limitations
   - Security considerations
   - What this class/method does NOT handle

8. **Complementary Components**
   - What should be used alongside this?
   - Integration patterns
   - Common compositions

9. **Historical Context** (if relevant)
   - Origin of the algorithm/pattern
   - Industry standards
   - Why certain defaults were chosen

10. **Return Value Documentation**
    - What does success look like?
    - What does failure look like?
    - Edge case behaviors
    - Observable side effects

**Formatting Requirements:**
- Use proper XML doc tags (<summary>, <remarks>, <para>, <list>, <code>, <example>)
- Use <list type="bullet"> for unordered lists
- Use <list type="number"> for ordered/sequential steps
- Use <list type="table"> for comparison tables
- Use <code> blocks for all code examples (include language if not C#)
- Escape XML special characters (&lt; &gt; &amp;)
- Keep line length reasonable (wrap at ~120 characters in remarks)
- Use emoji sparingly and only in examples/lists for visual scanning (🎯 ✅ ⚠️ 🔴 🟢)

**Inline Comment Requirements:**
- Add explanatory comments BEFORE complex logic blocks
- Explain the "why" not the "what" for non-obvious code
- Use full sentences with proper capitalization
- Add units to numeric calculations (e.g., "// Convert to milliseconds")
- Flag important decisions (e.g., "// Performance: Using linear search here because...")

**Tone:**
- Professional but approachable
- Assume reader is intelligent but may not know domain specifics
- Be prescriptive (tell them what to do, not just what's possible)
- Include warnings about misuse
- Celebrate best practices

**Special Sections for Specific Types:**

FOR TRADING SIGNALS:
- Add "Confidence Calculation" section with formula
- Add "Optimal Market Conditions" section
- Add "Parameter Tuning Guidelines" for different market types
- Include comparison with related signals

FOR INDICATORS:
- Add "Calculation Method" section with mathematical formula
- Add "Interpretation" section (what values mean)
- Include "Typical Values" or "Range" information

FOR FILTERS:
- Add "Filter Logic" section explaining pass/fail criteria
- Add "Composition Patterns" showing how to combine filters
- Include performance characteristics (fast/slow)

FOR BUILDERS/FACTORIES:
- Add "Builder Pattern" explanation
- Show complete fluent API example
- Document build validation rules

FOR PIPELINES/ORCHESTRATORS:
- Add "Execution Flow" diagram in comments
- Document error handling strategy
- Show trace/observability hooks

**Example Reference:**
Model after the RSIReversionSignal documentation which includes:
- 200+ line remarks section
- Multiple comparison tables
- Warning sections about misuse
- Exit strategy guidance
- Historical context
- 5+ usage examples

**Current Context:**
[Provide class signature, existing code, and any domain-specific information]

**Specific Focus Areas:**
[List any particular aspects you want emphasized: performance, security, common mistakes, integration patterns, etc.]
```


---

## **Shortened Version for Quick Documentation**

```
Add comprehensive XML documentation to [CLASS/METHOD NAME] in the style of TradeFlowGuardian.Strategies signals:
- Detailed <remarks> with algorithm explanation, use cases, and limitations
- 3+ <example> blocks with realistic code
- Parameter docs with ranges, defaults, and tuning advice
- Comparison tables where relevant (X vs Y)
- Inline comments explaining "why" for complex logic
- Professional but educational tone

Focus on: [performance/security/integration/etc.]
```


---

## **Usage Tips**

1. **For existing undocumented code:** Provide the full class/method and ask to add docs
2. **For new code generation:** Include this prompt BEFORE asking for implementation
3. **For consistency:** Reference specific examples from your codebase as style guides
4. **For updates:** Ask to "enhance existing documentation to match [reference class] verbosity"

## **Example Application**

```
Add comprehensive XML documentation to SmaIndicator following the same style as RSIReversionSignal:
- Explain SMA calculation method and formula
- Compare SMA vs EMA (when to use each)
- Include parameter tuning for different timeframes
- Add 3+ usage examples
- Document typical value ranges
- Add warnings about lag and whipsaw

Focus on: Making it beginner-friendly for traders new to technical analysis
```


This prompt template will help you maintain consistent, high-quality documentation across your entire codebase!