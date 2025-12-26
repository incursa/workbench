# Work Item Front Matter Format

## Overview

Work items use YAML front matter at the top of Markdown files to store structured metadata. This format is compatible with static site generators, many Markdown tools, and is easily parseable.

## Basic Structure

```markdown
---
id: TASK-0001
title: "Implement user authentication"
type: task
status: in-progress
priority: high
assignee: "@johndoe"
created: 2025-01-15T10:30:00Z
updated: 2025-01-16T14:22:00Z
tags:
  - authentication
  - security
  - backend
---

# Implement user authentication

## Description

We need to add JWT-based authentication to the API...

## Acceptance Criteria

- [ ] Users can register with email/password
- [ ] Users can login and receive JWT token
- [ ] Protected endpoints validate JWT tokens
```

## Required Fields

### id
- **Type**: string
- **Format**: `{PREFIX}-{NUMBER}` (e.g., `BUG-0001`, `TASK-0042`)
- **Description**: Unique identifier for the work item
- **Example**: `TASK-0123`

### title
- **Type**: string
- **Description**: Short, descriptive title of the work item
- **Example**: `"Fix null pointer exception in user service"`

### type
- **Type**: string (enum)
- **Values**: `bug`, `task`, `spike`
- **Description**: Category of work item
- **Example**: `task`

### status
- **Type**: string (enum)
- **Values**: `draft`, `ready`, `in-progress`, `review`, `done`, `cancelled`
- **Description**: Current lifecycle status
- **Example**: `in-progress`

### created
- **Type**: string (ISO 8601 datetime)
- **Description**: When the work item was created
- **Example**: `2025-01-15T10:30:00Z`

## Optional Fields

### priority
- **Type**: string (enum)
- **Values**: `critical`, `high`, `medium`, `low`
- **Default**: `medium`
- **Example**: `high`

### assignee
- **Type**: string
- **Format**: GitHub username (with or without @)
- **Description**: Person responsible for the work item
- **Example**: `@johndoe` or `johndoe`

### updated
- **Type**: string (ISO 8601 datetime)
- **Description**: When the work item was last modified
- **Example**: `2025-01-16T14:22:00Z`

### tags
- **Type**: array of strings
- **Description**: Labels for categorization and filtering
- **Example**: `["authentication", "security", "backend"]`

### estimate
- **Type**: string or number
- **Format**: Story points, hours, or t-shirt size
- **Description**: Effort estimate
- **Example**: `5` (story points) or `"2h"` or `"M"`

### branch
- **Type**: string
- **Description**: Associated Git branch
- **Example**: `feature/TASK-0123-user-auth`

### pr
- **Type**: string or number
- **Description**: Associated Pull Request number or URL
- **Example**: `42` or `https://github.com/org/repo/pull/42`

### related
- **Type**: array of strings
- **Description**: Related work item IDs
- **Example**: `["TASK-0120", "BUG-0015"]`

### blocked_by
- **Type**: array of strings
- **Description**: Work items blocking this one
- **Example**: `["TASK-0100", "SPIKE-0005"]`

### blocks
- **Type**: array of strings
- **Description**: Work items blocked by this one
- **Example**: `["TASK-0125", "TASK-0126"]`

## Type-Specific Fields

### Bug-Specific Fields

#### severity
- **Type**: string (enum)
- **Values**: `critical`, `major`, `minor`, `trivial`
- **Description**: Impact severity of the bug
- **Example**: `major`

#### environment
- **Type**: string
- **Description**: Where the bug occurs
- **Example**: `production` or `staging` or `dev`

#### reproduction
- **Type**: string
- **Description**: Steps to reproduce (can also be in Markdown body)
- **Example**: `"1. Login as admin\n2. Navigate to settings\n3. Click save"`

### Spike-Specific Fields

#### question
- **Type**: string
- **Description**: The question this spike answers
- **Example**: `"Which database technology should we use for time-series data?"`

#### timebox
- **Type**: string
- **Description**: Maximum time to spend on spike
- **Example**: `"2 days"` or `"8 hours"`

#### outcome
- **Type**: string (enum)
- **Values**: `recommendation`, `poc`, `document`, `decision`
- **Description**: Expected output of the spike
- **Example**: `recommendation`

## Examples

### Bug Example

```yaml
---
id: BUG-0042
title: "Null pointer exception when deleting user with no profile"
type: bug
status: ready
priority: high
severity: major
assignee: "@alice"
created: 2025-01-15T09:00:00Z
updated: 2025-01-15T09:00:00Z
tags:
  - user-management
  - crash
environment: production
related:
  - TASK-0050
---

# Null pointer exception when deleting user with no profile

## Description

When attempting to delete a user account that has no associated profile record,
the application throws a `NullPointerException`.

## Steps to Reproduce

1. Create a user account via API (bypassing profile creation)
2. Navigate to admin panel
3. Select the user
4. Click "Delete User"
5. Observe exception in logs

## Expected Behavior

User should be deleted successfully, whether or not a profile exists.

## Actual Behavior

```
NullPointerException at UserService.java:245
  at UserService.deleteUserProfile(UserService.java:245)
  at UserController.deleteUser(UserController.java:89)
```

## Impact

- Prevents deletion of orphaned user accounts
- Blocks admin workflow
- Requires manual database intervention
```

### Task Example

```yaml
---
id: TASK-0123
title: "Add pagination to user list API"
type: task
status: in-progress
priority: medium
assignee: "@bob"
created: 2025-01-10T14:30:00Z
updated: 2025-01-15T10:15:00Z
estimate: 5
tags:
  - api
  - performance
  - backend
branch: feature/TASK-0123-pagination
related:
  - TASK-0100
---

# Add pagination to user list API

## Description

The `/api/users` endpoint currently returns all users in a single response,
which becomes slow with large user counts. Add pagination support.

## Requirements

- Support `page` and `pageSize` query parameters
- Default page size: 50
- Max page size: 200
- Include pagination metadata in response

## Acceptance Criteria

- [ ] Add pagination parameters to endpoint
- [ ] Return paginated results
- [ ] Include total count in response
- [ ] Add unit tests for pagination logic
- [ ] Update API documentation
- [ ] Test with 10k+ user dataset

## API Design

```
GET /api/users?page=1&pageSize=50

Response:
{
  "data": [...],
  "pagination": {
    "page": 1,
    "pageSize": 50,
    "total": 1234,
    "totalPages": 25
  }
}
```
```

### Spike Example

```yaml
---
id: SPIKE-0007
title: "Evaluate real-time communication options for chat feature"
type: spike
status: in-progress
priority: high
assignee: "@charlie"
created: 2025-01-12T11:00:00Z
updated: 2025-01-14T16:30:00Z
question: "What technology should we use for real-time chat: WebSockets, SSE, or long polling?"
timebox: "3 days"
outcome: recommendation
tags:
  - architecture
  - real-time
  - research
---

# Evaluate real-time communication options for chat feature

## Objective

Determine the best approach for implementing real-time chat functionality
in our application.

## Options to Investigate

1. **WebSockets** (Socket.IO, SignalR)
2. **Server-Sent Events (SSE)**
3. **Long Polling**
4. **Third-party services** (Pusher, Ably)

## Evaluation Criteria

- Browser compatibility
- Server resource usage
- Scalability (to 10k concurrent users)
- Development complexity
- Hosting costs
- Fallback mechanisms

## Deliverable

A recommendation document with:
- Comparison matrix
- Proof-of-concept code
- Cost estimates
- Proposed architecture
```

## Validation Rules

Work items must pass these validation rules:

1. **Front matter exists**: File must start with `---` and contain valid YAML
2. **Required fields present**: All required fields must be present
3. **Valid ID format**: ID must match `{PREFIX}-{DIGITS}` pattern
4. **Valid status**: Status must be one of the allowed values
5. **Valid type**: Type must be `bug`, `task`, or `spike`
6. **Valid dates**: Created/updated must be valid ISO 8601 dates
7. **Created before updated**: If both dates present, created <= updated
8. **Unique ID**: No two work items can have the same ID

## Parsing Implementation

Workman uses **YamlDotNet** to parse front matter:

```csharp
var input = File.ReadAllText(filePath);
var match = Regex.Match(input, @"^---\s*\n(.*?)\n---\s*\n(.*)$", 
    RegexOptions.Singleline);

if (match.Success)
{
    var yaml = match.Groups[1].Value;
    var markdown = match.Groups[2].Value;
    
    var deserializer = new DeserializerBuilder().Build();
    var frontMatter = deserializer.Deserialize<WorkItemFrontMatter>(yaml);
    
    // Validate and process...
}
```

## Best Practices

1. **Keep titles concise**: 60-80 characters max
2. **Use tags consistently**: Establish a taxonomy early
3. **Update timestamps**: Always update `updated` when modifying
4. **Link related items**: Use `related` to connect work
5. **Describe clearly**: The Markdown body should provide full context
6. **Track blockers**: Use `blocked_by` to highlight dependencies
