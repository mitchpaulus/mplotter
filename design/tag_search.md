# Tag searching

- text box below the name search
- keyboard driven (`t` to put cursor in search filter)
- split the search on spaces, then do case insensitive exact string match. Multiple tokens implies AND
- help with autocomplete, tab or enter to take auto-complete suggestion. Should not replace previous tokens
- I want to be able to see multiple suggestions for discoverability
- suggestions from built in tags and any custom tags that are currently found
- suggestion ordering:
  - 1. prefix matches on the current token first
  - 2. then other matches
