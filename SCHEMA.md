# YCSS Schema Specification

Version: 1.0.0

This document defines the schema for YCSS YAML files, including validation rules, required properties, and formatting standards.

## Root Structure

The root of a YCSS file must contain at least one of these sections:
- `tokens`: Design token definitions
- `components`: Component style definitions

Optional fields:
- `version`: Schema version in format `major.minor.patch`

Example:
```yaml
version: 1.0.0
tokens:
  # Token definitions
components:
  # Component definitions
```

## Tokens

### Format

Tokens must be defined as key-value pairs where:
- Keys must be kebab-case and start with a letter
- Values must conform to their inferred type

Example:
```yaml
tokens:
  color-primary: "#1f2937"
  spacing-lg: "2rem"
  font-size-base: "16px"
```

### Token Types

The type of a token is inferred from its prefix:

1. **Color Tokens** (prefix: `color-`)
   - Valid formats:
     - Hex: `#RGB` or `#RRGGBB`
     - RGB: `rgb(R, G, B)`
   - Example: `color-primary: "#1f2937"`

2. **Spacing Tokens** (prefix: `spacing-`)
   - Valid units: px, rem, em
   - Example: `spacing-lg: "2rem"`

3. **Typography Tokens** (prefix: `font-`)
   - Size: Must include unit (px, rem, em)
   - Weight: Must be "normal", "bold", or 100-900
   - Example: `font-size-lg: "18px"`

4. **Border Tokens** (prefix: `border-`)
   - Format: `<width> <style> <color>`
   - Example: `border-default: "1px solid #000"`

## Components

### Basic Structure

Components must have:
- A valid component name (kebab-case)
- A `styles` section
- Optional `class` name
- Optional `variants` section

Example:
```yaml
components:
  button:
    class: button
    styles:
      - background-color: "#000"
      - padding: "1rem"
    variants:
      primary:
        class: button--primary
        styles:
          - background-color: "#007bff"
```

### Class Names

Class names must follow either:
- Kebab-case: `my-component`
- BEM format: 
  - Block: `block`
  - Element: `block__element`
  - Modifier: `block--modifier` or `block__element--modifier`

### Required Properties

Some components have required properties that must be present in their styles:

#### Button
Required:
- `background-color`
- `padding`

Recommended:
- `border-radius`
- `font-weight`

#### Input
Required:
- `border`
- `padding`

Recommended:
- `border-radius`
- `width`

#### Card
Required:
- `padding`
- `background-color`

Recommended:
- `border-radius`
- `box-shadow`

#### Modal
Required:
- `position`
- `background-color`

Recommended:
- `width`
- `height`
- `z-index`

#### Grid Container
Required:
- `display: grid`
- `grid-template-columns`

Recommended:
- `gap`
- `width`

#### Flex Container
Required:
- `display: flex`
- `flex-direction`

Recommended:
- `justify-content`
- `align-items`

### CSS Property Validation

The following CSS properties are validated:

#### Colors
- `color`
- `background-color`
```yaml
# Valid formats
color: "#123456"
color: "#123"
color: "rgb(255, 128, 0)"
color: "var(--color-token)"
```

#### Spacing
- `margin`
- `padding`
```yaml
# Valid formats
margin: "16px"
margin: "1rem"
margin: "1.5em"
margin: "10%"
margin: "var(--spacing-token)"
```

#### Sizing
- `width`
- `height`
```yaml
# Valid formats
width: "100px"
width: "50%"
width: "100vw"
width: "auto"
width: "var(--width-token)"
```

#### Typography
- `font-size`
- `font-weight`
- `line-height`
```yaml
# Valid formats
font-size: "16px"
font-weight: "bold"
font-weight: "400"
line-height: "1.5"
```

#### Borders
- `border`
- `border-radius`
```yaml
# Valid formats
border: "1px solid #000"
border-radius: "4px"
```

#### Layout
- `display`
```yaml
# Valid values
display: "block"
display: "inline"
display: "flex"
display: "grid"
display: "none"
```

#### Position
- `position`
```yaml
# Valid values
position: "static"
position: "relative"
position: "absolute"
position: "fixed"
position: "sticky"
```

#### Flexbox
- `flex-direction`
- `justify-content`
- `align-items`
```yaml
# Valid values
flex-direction: "row"
flex-direction: "column"
justify-content: "center"
justify-content: "space-between"
align-items: "center"
```

#### Grid
- `grid-template-columns`
- `gap`
```yaml
# Valid formats
grid-template-columns: "1fr 1fr 1fr"
grid-template-columns: "repeat(3, 1fr)"
gap: "16px"
```

## Variants

Variants extend or modify base components:

- Must have a valid kebab-case name
- Must include `class` name (typically BEM modifier)
- Must include `styles` section
- Inherit validation rules from parent component

Example:
```yaml
components:
  button:
    class: button
    styles:
      - background-color: "#000"
    variants:
      primary:
        class: button--primary
        styles:
          - background-color: "#007bff"
      outline:
        class: button--outline
        styles:
          - background-color: "transparent"
          - border: "1px solid #000"
```

## Error Handling

Validation issues are reported with:
- Path to the problematic node
- Descriptive error message
- Severity level (ERROR or WARNING)

Example output:
```
ERROR   components.button: Required property 'background-color' is missing
WARNING components.card: Recommended property 'border-radius' is missing
ERROR   tokens.123-invalid: Token name must be kebab-case and start with a letter
```

## Best Practices

1. **Token Usage**
   - Prefer using design tokens via `var(--token-name)` over hard-coded values
   - Follow token naming conventions for better maintainability

2. **Component Organization**
   - Group related components together
   - Use consistent naming patterns
   - Document component relationships

3. **Style Structure**
   - Order properties logically (layout → box model → visual)
   - Keep styles focused and minimal
   - Use variants for variations rather than new components

4. **Validation**
   - Run validation before committing changes
   - Address all errors and review warnings
   - Keep component styles conforming to their required properties