### Part 1

### Reflection questions

1. If you wanted to verify that a `Published` meeting gets `badge-published` and
   a `Draft` meeting gets `badge-draft` — what would you need to set up?

2. In `RegistrationsEndpoints.cs`, the registration timestamp is
   `DateTimeOffset.UtcNow`. If you wrote a test that checks the timestamp,
   would it be deterministic? Why or why not?

3. `MeetingDetailsPage.tsx` fetches data with `fetchMeeting(id)`, computes the
   average rating, and renders everything. If you only want to test the average
   rating calculation, what is the minimum setup you would need?

### Answers

1. I would need to create 2 mock `Meeting` objects each one with the needed status. Because of the <link>  I'll wrap the componeted in a memory router. Then I would check if the rendered badge displays the correct status text and has the matching badge class.

2.  No. The value of timestamp depends on the time of the machine running it so testing it would be very risky as you can't pinpoint what the expected result is supposed to be.

3. Because the the average rating is computed in the page testing it would require more work than refactoring the code.
   The bear minimum setup would be to mock the fecthMeeting() function so we could get knowns ratings. Then setup a router with an id because of useParams(). Then render the page, wait for it to finish rendering and sending the request and only then test the assertion.

### Part 3

### Questions to think about while working

1. How many fields did you have to fake just to test the badge?

2. Could you test the badge logic without rendering React at all?

3. If someone changes the badge logic, would your test catch it? What if they change the CSS class but not the text?

### Answers

1. I had to fake all 11 fields of the meeting object. I created a single object using a factory function and then changed it's status field before each test to match up the badge that is being tested.

2. You can't test the badge logic without rending React. You can't extract text from the page or check what badges is shown. The tested variable is also computed inside the React component. To summarize without rendering the line I'm testing wouldn't be executed so there would be nothing to test.

3. It depends on the changes:

   - If the CSS class in the JSX is changed but the text is untouched, the text assertion would still pass. But the class assertion would fail.

   - If the comparison strings are changed (for example meeting.status === "Published" becomes meeting.status === "Live"). It would cause a badgeClass mismatch causing the test to fail. 

   - If line 19 itself changed so that the component stopped rendering meeting.status as visible text or renamed the displayed string so "Published" no longer appears in the component at all. The test would crash and return an error.

   - If classes are swapped (for example "badge-draft Draft" gets "badge-published"). The tests would fail due to a class assertion mismatch.

   - If the CSS class name changes (for example "badge-published" changes to "badge-live`) that would be caught due to a class assertion mismatch.

   - If instead the actual CSS styling changes the test has no way to catch that. toHaveClass only checks the class attribute string and not computed visual styling.

### Part 4

### You will run into problems. That is expected. Write down what blocks you:

- Can you call the endpoint logic without starting the web server?

- Can you test the validation without hitting the real database?

- Can you control what `DateTimeOffset.UtcNow` returns?

- If you could extract the validation into a separate method/class, what would its signature look like?

### Blockers

1. I can't test the endpoint without running the web server. There is no standalone method to invoke so the only way to execute the code would be to send a real HTTP request

2. Can't test validtion with an a real database. The endpoint saves objects to a real database. I would need to mock or access the databse used by the code to properly test registration validation

3. Using a static time stamp makes the test unreliable this was discussed in Part 1, question 2. I would use System.TimeProvider and a fake timeprovider for testing

4. I would use something like this:

   public static RegistrationValidationResult Validate(string meetingStatus, int currentRegistrationCount, int venueCapacity)

   public record RegistrationValidationResult(bool IsAccepted, string? RejectionReason) {
      public static RegistrationValidationResult Accepted() => new(true, null);
      public static RegistrationValidationResult Rejected(string reason) => new(false, reason);
   }

   This way I only need to handly primitive values and don't have to deal the DbContext. It would allow to test each buiness rule sperately and make the tests easier and shorter to write