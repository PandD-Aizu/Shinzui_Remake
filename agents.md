# Agents.md

## Coding Strategy
- When adjusting or modifying existing functions, strive to make the changes to a minimum
- Avoid excessive exception handling

## Code Comment Strategy
- Functions should be accompanied by XML-style comments
  - Example: /// <summary> ○○ <summary/> (Of course, add line breaks for readability)
  - Return value
  - Arguments should also be written
- Insert blank lines appropriately between each block of processing
- Add comments explaining what each block of processing does
- Punctuation at the end of sentences is unnecessary
- Do not use polite forms like "desu" or "masu"

## Tests
- Do not perform more tests than necessary.
  - You don't need to perform more than 100 tests.
  - Aim for the minimum necessary tests.
- Do not write more documentation than necessary.
  - Do not keep a record of erasing your bugs.
  - Simply tell the user what the bug was and how it was fixed.