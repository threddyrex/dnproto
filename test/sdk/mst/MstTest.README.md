# MST Test - Bluesky Comparison Tests

## Overview

The [MstTest.cs](./MstTest.cs) test file contains comprehensive tests that compare dnproto's `MstRepository` implementation against actual Bluesky-generated repository CAR files.

## Test Structure

The tests simulate a typical Bluesky user workflow with 7 distinct steps:

1. **New Account** - Empty repository after account creation
2. **Login** - Repository state after login (should be unchanged)
3. **Set Profile** - Repository with a profile record
4. **First Post** - Repository with profile + first post
5. **First Like** - Repository with profile + post + like
6. **Reply** - Repository with profile + post + like + reply
7. **Handle Change** - Repository after handle change (no structural change)

## Running the Tests

### Without Bluesky CAR Files

If you don't have Bluesky CAR files, the comparison tests will be skipped, but the integration test will still run:

```bash
cd D:\bld\src\dnproto\test
dotnet test --filter "FullyQualifiedName~MstTest"
```

This will run:
- `FullWorkflow_AllSteps_RepositoryIsValid` - Verifies dnproto's implementation works correctly through all steps

### With Bluesky CAR Files

To run the full comparison tests:

1. Place your Bluesky CAR files in `test/data/msttest/` with these names:
   - `msttest-01-new.car`
   - `msttest-02-login.car`
   - `msttest-03-setprofile.car`
   - `msttest-04-firstpost.car`
   - `msttest-05-firstlike.car`
   - `msttest-06-reply.car`
   - `msttest-07-handlechange.car`

2. Run the tests:
   ```bash
   cd D:\bld\src\dnproto\test
   dotnet test --filter "FullyQualifiedName~MstTest"
   ```

## How to Get Bluesky CAR Files

To create the Bluesky comparison files:

1. Create a new test account on Bluesky
2. After each step, download your repository:
   ```bash
   curl "https://bsky.social/xrpc/com.atproto.sync.getRepo?did=YOUR_DID" -o msttest-XX-stepname.car
   ```
3. Place the CAR files in `test/data/msttest/`

## What the Tests Verify

Each test compares:

- **Record Count** - Same number of records in both repos
- **Record Paths** - Same collection paths (e.g., `app.bsky.feed.post/3kj1abc`)
- **MST Structure** - Similar node and record counts
- **Repository Integrity** - Root CID is computed correctly

## Test Features

### Helper Methods

The test file includes helper methods for creating AT Protocol records:

- `CreateProfileRecord()` - Creates `app.bsky.actor.profile` records
- `CreatePostRecord()` - Creates `app.bsky.feed.post` records
- `CreateLikeRecord()` - Creates `app.bsky.feed.like` records
- `CreateReplyRecord()` - Creates `app.bsky.feed.post` records with reply metadata

### Comparison Logic

The `CompareRepositories()` method performs detailed comparisons:

- Compares sorted record lists
- Validates MST statistics
- Checks root CID computation
- Logs detailed information for debugging

## Expected Results

### Without CAR Files
```
Test Results: 1 passed, 7 skipped

✓ FullWorkflow_AllSteps_RepositoryIsValid
⊘ Step01_NewAccount_MatchesBluesky (Skipped)
⊘ Step02_Login_MatchesBluesky (Skipped)
⊘ Step03_SetProfile_MatchesBluesky (Skipped)
⊘ Step04_FirstPost_MatchesBluesky (Skipped)
⊘ Step05_FirstLike_MatchesBluesky (Skipped)
⊘ Step06_Reply_MatchesBluesky (Skipped)
⊘ Step07_HandleChange_MatchesBluesky (Skipped)
```

### With CAR Files
```
Test Results: 8 passed

✓ Step01_NewAccount_MatchesBluesky
✓ Step02_Login_MatchesBluesky
✓ Step03_SetProfile_MatchesBluesky
✓ Step04_FirstPost_MatchesBluesky
✓ Step05_FirstLike_MatchesBluesky
✓ Step06_Reply_MatchesBluesky
✓ Step07_HandleChange_MatchesBluesky
✓ FullWorkflow_AllSteps_RepositoryIsValid
```

## Notes

- Tests use a simple identity signer (returns input hash) for testing purposes
- Actual production code should use proper cryptographic signing
- CID comparisons are flexible since timestamps and other details may differ
- The main goal is structural compatibility, not byte-for-byte identity

## Troubleshooting

If tests fail:

1. **Check CAR file locations** - Files must be in `test/data/msttest/`
2. **Verify file names** - Must match exactly (e.g., `msttest-01-new.car`)
3. **Check DID consistency** - All CAR files should be from the same account
4. **Review record counts** - Ensure each step adds the expected records

## Future Enhancements

Potential additions to these tests:

- Compare exact record content (DAG-CBOR)
- Validate CID computation matches Bluesky exactly
- Test more complex scenarios (reposts, blocks, follows)
- Performance benchmarks
- MST tree structure visualization
