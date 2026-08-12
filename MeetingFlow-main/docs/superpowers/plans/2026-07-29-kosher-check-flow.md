# Kosher Check Flow Implementation Plan

> **For Codex:** Execute this plan one task at a time. Write a failing test before each production change, then make the smallest change that passes it.

**Goal:** Add a monolith-only page that checks 1–10 dish descriptions through the existing AI provider and returns a genuine JSON-schema-based structured result.

**Architecture:** A Razor Page collects dishes and calls a small application service. The service uses `Microsoft.Extensions.AI` typed structured output, validates that every requested dish has exactly one result, and returns neutral domain models. Browser JavaScript manages dynamic fields, the non-streaming request, loading state, safe rendering, and local history.

**Tech Stack:** .NET 10, ASP.NET Core Razor Pages, Microsoft.Extensions.AI.OpenAI 10.3.0, vanilla JavaScript, CSS, xUnit.

---

### Task 1: Domain contract and input validation

**Files:**
- Create: `MeetingFlow.Monolith/Models/KosherAssessment.cs`
- Create: `MeetingFlow.Monolith/Services/KosherInputValidator.cs`
- Create: `MeetingFlow.Monolith.Tests/MeetingFlow.Monolith.Tests.csproj`
- Create: `MeetingFlow.Monolith.Tests/KosherInputValidatorTests.cs`

**Steps:**
1. Add tests for 1–10 trimmed, non-empty descriptions with a 500-character limit.
2. Run the focused test and confirm it fails because the implementation is absent.
3. Add the minimal models and validator.
4. Run the focused test and confirm it passes.

### Task 2: Typed structured-output AI service

**Files:**
- Create: `MeetingFlow.Monolith/Services/IKosherAssessmentService.cs`
- Create: `MeetingFlow.Monolith/Services/OpenAiKosherAssessmentService.cs`
- Create: `MeetingFlow.Monolith/Services/KosherAssessmentException.cs`
- Create: `MeetingFlow.Monolith.Tests/OpenAiKosherAssessmentServiceTests.cs`
- Modify: `MeetingFlow.Monolith/MeetingFlow.Monolith.csproj`

**Steps:**
1. Add a fake `IChatClient` test proving that the service requests a JSON-schema response format.
2. Add tests for exact dish identifiers, allowed statuses, non-empty explanations, and hostile dish text being treated as data.
3. Run the tests and confirm they fail.
4. Implement a single non-streaming typed `GetResponseAsync<T>` call and semantic result validation.
5. Run the focused tests and confirm they pass.

### Task 3: Razor Page endpoint and configuration

**Files:**
- Create: `MeetingFlow.Monolith/Pages/KosherCheck.cshtml.cs`
- Create: `MeetingFlow.Monolith/Services/UnavailableKosherAssessmentService.cs`
- Create: `MeetingFlow.Monolith.Tests/KosherCheckPageTests.cs`
- Modify: `MeetingFlow.Monolith/Program.cs`
- Modify: `MeetingFlow.Monolith/appsettings.json`

**Steps:**
1. Add handler tests for valid input, invalid input, unavailable AI, and safe generic errors.
2. Run the tests and confirm they fail.
3. Implement the page handler and register the AI client from `AiChat` configuration only when a key is present.
4. Keep the rest of the monolith runnable when AI configuration is absent.
5. Run the focused tests and confirm they pass.

### Task 4: Page, browser interaction, and local history

**Files:**
- Create: `MeetingFlow.Monolith/Pages/KosherCheck.cshtml`
- Create: `MeetingFlow.Monolith/wwwroot/js/kosher-check.js`
- Create: `MeetingFlow.Monolith.Tests/Browser/kosher-check.test.mjs`
- Modify: `MeetingFlow.Monolith/Pages/Shared/_Layout.cshtml`
- Modify: `MeetingFlow.Monolith/wwwroot/css/site.css`

**Steps:**
1. Add JavaScript tests for the 10-field limit, local-history cap/order, and status normalization.
2. Run the tests with Node and confirm they fail.
3. Add the accessible English-only form, add/remove controls, loading state, result table, disclaimer, and history view.
4. Render all user/model values through `textContent`.
5. Run JavaScript tests and .NET tests.

### Task 5: Documentation and verification

**Files:**
- Modify: `MeetingFlow.Monolith/README.md`
- Modify: `implementation-notes.md`

**Steps:**
1. Document required `AiChat__Model`, `AiChat__Endpoint`, and `AiChat__ApiKey` environment variables.
2. Verify build and all test suites.
3. Start the monolith without an AI key and verify existing pages still load.
4. Review the diff for accidental secrets, forbidden DTO-style names, streaming calls, or monolith-boundary violations.
5. Record remaining environmental limitations and prepare the local branch for review.
