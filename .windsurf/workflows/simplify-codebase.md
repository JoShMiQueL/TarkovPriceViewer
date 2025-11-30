---
description: Analyze and simplify the codebase to improve maintainability, readability, and developer experience.
auto_execution_mode: 1
---

# Simplify Codebase

Analyze and simplify the codebase to improve maintainability, readability, and developer experience. This command will:

1. **Remove Dead Code & Dependencies**
   - Find unused imports, functions, and variables
   - Identify unused npm dependencies
   - Remove commented-out code blocks
   - Clean up empty files and directories

2. **Optimize Package Scripts**
   - Review package.json scripts for redundancy
   - Ensure scripts have clear, logical purposes
   - Remove wrapper scripts that don't add value
   - Consolidate similar scripts where appropriate

3. **Code Structure Analysis**
   - Identify overly complex files that should be split
   - Find duplicate code that can be extracted
   - Review folder structure for logical organization
   - Suggest component/module consolidations

4. **Developer Experience Improvements**
   - Ensure consistent code formatting and style
   - Add missing TypeScript types where needed
   - Simplify complex logic and improve readability
   - Optimize import statements and module organization

5. **Documentation & Onboarding**
   - Update CLAUDE.md with simplified architecture notes
   - Ensure development commands are clear and necessary
   - Remove excessive comments while keeping essential ones
   - Verify project setup instructions are accurate

## Process

1. Analyze the current codebase structure and identify areas for improvement
2. Create a plan for simplification focusing on the biggest impact areas
3. Execute changes systematically while maintaining functionality
4. Update documentation to reflect the simplified structure
5. Verify all scripts and development workflows still work correctly

Focus on making the codebase more approachable for new contributors while maintaining all existing functionality.