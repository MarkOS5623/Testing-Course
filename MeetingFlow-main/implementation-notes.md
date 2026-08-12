# Implementation Notes

## Deviations

- The monolith needs a direct `Microsoft.Extensions.AI` package reference in addition to
  `Microsoft.Extensions.AI.OpenAI`; the typed `GetResponseAsync<T>` helper is provided by
  the former package.
- A full `dotnet test MeetingFlow.slnx` run cannot pass in this local environment because
  the existing microservice integration tests require the Docker gateway at
  `http://localhost:8080`. Monolith tests are run separately.

## Discovered edge cases

- Disabled HTML form fields are omitted from `FormData`, so the browser code captures
  `FormData` before disabling controls for the loading state.
- Missing AI configuration must not activate the microservice's unrelated rule-based
  fallback. A dedicated unavailable service keeps the monolith runnable.
- Model output order is not trusted. Results are validated by generated dish identifiers
  and reordered to match the user's original input.
- `JsonStringEnumConverter` accepts integer enum values by default. Integer values are
  disabled explicitly and status membership is checked again after deserialization.
- A failed repeated check must hide the previous result so that stale output is not
  mistaken for the new answer.
- A rate-limit attribute on the Razor Page model also counts GET requests, while an
  attribute on an individual Razor Page handler is not applied as endpoint metadata.
  The final middleware branch therefore runs only for `POST /KosherCheck`.

## Questions for review

- The original AI engine uses an older GitHub Models endpoint. For local testing, the
  monolith uses OpenAI `https://api.openai.com/v1` with `gpt-5-mini`, which supports
  JSON-schema structured output and is suitable for this small classification task.
- The lightweight abuse protection is process-local. A multi-instance deployment should
  use a shared gateway-level quota in addition to the per-instance controls.
