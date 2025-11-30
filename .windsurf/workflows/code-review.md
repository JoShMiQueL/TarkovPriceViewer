---
description: Perform comprehensive code review focusing on performance, best practices, linting, formatting, and optimization. Analyze the codebase for improvements and apply fixes systematically.
auto_execution_mode: 1
---

# Code Review & Optimization

Perform comprehensive code review focusing on performance, best practices, linting, formatting, and optimization. Analyze the codebase for improvements and apply fixes systematically.

## Analysis Phase

1. **Codebase Overview**
   - Identify files that exceed complexity thresholds (>400 lines, >300 for components)
   - Check for unused imports, variables, and dead code
   - Analyze component structure and decomposition opportunities
   - Review state management patterns and store organization

2. **Performance Analysis**
   - Identify potential performance bottlenecks
   - Check for unnecessary re-renders and computations
   - Review bundle size and import efficiency
   - Analyze database query patterns and optimization opportunities

3. **Code Quality Assessment**
   - TypeScript usage and type safety
   - Error handling patterns
   - Code duplication and reusability
   - Naming conventions and clarity

## Implementation Phase

4. **Linting & Formatting**
   - Run `npm run lint` and fix all issues
   - Run `npm run format` for consistent code style
   - Address TypeScript errors with `npm run type-check`
   - Fix import organization and remove unused imports

5. **Optimization Fixes**
   - Extract reusable logic into composables
   - Decompose large components (>300 lines) into smaller, focused ones
   - Consolidate duplicate code patterns
   - Optimize import statements using absolute paths with `@/` prefix

6. **Best Practices Implementation**
   - Ensure proper TypeScript casting for Pinia stores: `as StoreWithFireswapExt<ReturnType<typeof useStore>>`
   - Implement proper null checking for optional values
   - Replace `any` types with proper interfaces
   - Use Vue 3 Composition API patterns consistently

7. **Performance Improvements**
   - Add memoization where appropriate
   - Optimize component props and events for clear interfaces
   - Reduce template nesting and complexity
   - Implement lazy loading for large components

## Validation Phase

8. **Testing & Verification**
   - Run the complete test suite to ensure no regressions
   - Test build process: `npm run build`
   - Verify development environment: `npm run dev`
   - Check that all functionality remains intact

## Rules & Constraints

- **Avoid "ignore rule" comments** - Fix the underlying issue instead
- **Preserve functionality** - All existing features must continue working
- **Follow project patterns** - Use existing conventions and architectures
- **Maintain Vue 3 + TypeScript + Vuetify patterns**
- **Keep changes focused** - Don't introduce new features during review
- **Use absolute imports** with `@/` prefix consistently
- **Follow monorepo structure** - respect frontend/functions separation

## Output Format

Provide a summary of:
- Issues identified and fixed
- Performance improvements made
- Code quality enhancements
- Files refactored and why
- Any remaining technical debt or recommendations

Focus on measurable improvements and maintain the existing codebase architecture while enhancing its quality and performance.