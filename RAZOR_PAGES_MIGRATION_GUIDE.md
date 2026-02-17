# Razor Pages Migration Guide
## Converting onclick to data-action Attributes

This guide shows how to migrate inline onclick handlers to the modern data-action pattern.

## Quick Reference

### Before (Old Pattern)
```html
<button onclick="replyToPost('post123', 'John Doe')">Reply</button>
<button onclick="toggleReaction('post123', 'ThumbsUp')">👍</button>
<button onclick="editPost('post123', 'user456')">Edit</button>
```

### After (New Pattern)
```html
<button data-action="reply-to-post"
        data-post-id="post123"
        data-author-name="John Doe">Reply</button>

<button data-action="toggle-reaction"
        data-post-id="post123"
        data-reaction-type="ThumbsUp">👍</button>

<button data-action="edit-post"
        data-post-id="post123"
        data-user-id="user456">Edit</button>
```

## Complete Conversion Table

### Editor Actions

```html
<!-- Preview Toggle -->
<!-- OLD --> <button onclick="togglePreview(true)">Preview</button>
<!-- NEW --> <button data-action="toggle-preview" data-show="true">Preview</button>

<!-- Bold Text -->
<!-- OLD --> <button onclick="insertMarkup('**', '**')">Bold</button>
<!-- NEW --> <button data-action="insert-bold">Bold</button>

<!-- Italic Text -->
<!-- OLD --> <button onclick="insertMarkup('*', '*')">Italic</button>
<!-- NEW --> <button data-action="insert-italic">Italic</button>

<!-- Insert Link -->
<!-- OLD --> <button onclick="insertMarkup('[', '](url)')">Link</button>
<!-- NEW --> <button data-action="insert-link">Link</button>

<!-- Insert Code -->
<!-- OLD --> <button onclick="insertMarkup('`', '`')">Code</button>
<!-- NEW --> <button data-action="insert-code">Code</button>

<!-- Insert List -->
<!-- OLD --> <button onclick="insertLinePrefix('- ')">List</button>
<!-- NEW --> <button data-action="insert-list">List</button>
```

### Reply Actions

```html
<!-- Reply to Post -->
<!-- OLD -->
<button onclick="replyToPost('@post.PublicId', '@post.Author.DisplayName')">Reply</button>

<!-- NEW -->
<button data-action="reply-to-post"
        data-post-id="@post.PublicId"
        data-author-name="@post.Author.DisplayName">Reply</button>

<!-- Quote Post -->
<!-- OLD -->
<button onclick="quotePost('@post.PublicId', `@post.Content`, '@post.Author.DisplayName')">Quote</button>

<!-- NEW -->
<button data-action="quote-post"
        data-post-id="@post.PublicId"
        data-content="@post.Content"
        data-author-name="@post.Author.DisplayName">Quote</button>

<!-- Clear Reply Context -->
<!-- OLD -->
<button onclick="clearReplyContext()">Cancel Reply</button>

<!-- NEW -->
<button data-action="clear-reply-context">Cancel Reply</button>
```

### Post Actions

```html
<!-- Edit Post -->
<!-- OLD -->
<button onclick="editPost('@post.PublicId', '@Model.CurrentUserId')">Edit</button>

<!-- NEW -->
<button data-action="edit-post"
        data-post-id="@post.PublicId"
        data-user-id="@Model.CurrentUserId">Edit</button>

<!-- Submit Edit -->
<!-- OLD -->
<button onclick="submitEdit('@post.PublicId', '@userId')">Save</button>

<!-- NEW -->
<button data-action="submit-edit"
        data-post-id="@post.PublicId"
        data-user-id="@userId">Save</button>

<!-- Cancel Edit -->
<!-- OLD -->
<button onclick="cancelEdit('@post.PublicId')">Cancel</button>

<!-- NEW -->
<button data-action="cancel-edit"
        data-post-id="@post.PublicId">Cancel</button>

<!-- Highlight Post -->
<!-- OLD -->
<a href="#post-@post.PublicId" onclick="highlightPost('@post.PublicId')">Jump to post</a>

<!-- NEW -->
<a href="#post-@post.PublicId"
   data-action="highlight-post"
   data-post-id="@post.PublicId">Jump to post</a>
```

### Reaction Actions

```html
<!-- Toggle Reaction Picker -->
<!-- OLD -->
<button onclick="toggleReactionPicker('@post.PublicId')" title="Add reaction">+</button>

<!-- NEW -->
<button data-action="toggle-reaction-picker"
        data-post-id="@post.PublicId"
        title="Add reaction">+</button>

<!-- Toggle Specific Reaction -->
<!-- OLD -->
<button onclick="toggleReaction('@postId', 'ThumbsUp')">👍</button>

<!-- NEW -->
<button data-action="toggle-reaction"
        data-post-id="@postId"
        data-reaction-type="ThumbsUp">👍</button>
```

### Discussion Actions

```html
<!-- Follow Discussion -->
<!-- OLD -->
<button onclick="toggleFollowDiscussion('@Model.DiscussionId')">Follow</button>

<!-- NEW -->
<button data-action="toggle-follow-discussion"
        data-discussion-id="@Model.DiscussionId">Follow</button>

<!-- Mute Discussion -->
<!-- OLD -->
<button onclick="toggleMuteDiscussion('@Model.DiscussionId')">Mute</button>

<!-- NEW -->
<button data-action="toggle-mute-discussion"
        data-discussion-id="@Model.DiscussionId">Mute</button>

<!-- Jump to Unread -->
<!-- OLD -->
<button onclick="jumpToUnread()">Jump to Unread</button>

<!-- NEW -->
<button data-action="jump-to-unread">Jump to Unread</button>
```

### User Actions

```html
<!-- Hide Posts From User -->
<!-- OLD -->
<button onclick="hidePostsFromUser('@user.PublicId', '@user.DisplayName')">Hide posts</button>

<!-- NEW -->
<button data-action="hide-posts-from-user"
        data-user-id="@user.PublicId"
        data-user-name="@user.DisplayName">Hide posts</button>

<!-- Unhide User -->
<!-- OLD -->
<button onclick="unhideUser('@userId')">Undo</button>

<!-- NEW -->
<button data-action="unhide-user"
        data-user-id="@userId">Undo</button>
```

### Load Actions

```html
<!-- Load More Posts -->
<!-- OLD -->
<button onclick="loadMorePosts('@discussionId', '@currentUserId', @isAuth, @isLocked)">Load More</button>

<!-- NEW -->
<button data-action="load-more-posts"
        data-discussion-id="@discussionId"
        data-current-user-id="@currentUserId"
        data-is-authenticated="@isAuth.ToString().ToLower()"
        data-is-locked="@isLocked.ToString().ToLower()">Load More</button>

<!-- Retry Load Posts -->
<!-- OLD -->
<button onclick="retryLoadPosts('@discussionId', '@userId', true, false, true)">Retry</button>

<!-- NEW -->
<button data-action="retry-load-posts"
        data-discussion-id="@discussionId"
        data-current-user-id="@userId"
        data-is-authenticated="true"
        data-is-locked="false"
        data-prefer-endless-scroll="true">Retry</button>
```

### Report Actions

```html
<!-- Open Report Modal -->
<!-- OLD -->
<button onclick="openReportModal('post', '@post.PublicId', '@description', '@spaceId')">Report</button>

<!-- NEW -->
<button data-action="open-report-modal"
        data-type="post"
        data-target-id="@post.PublicId"
        data-description="@description"
        data-space-id="@spaceId">Report</button>

<!-- Submit Report Form -->
<!-- OLD -->
<form onsubmit="submitReport(event); return false;">

<!-- NEW -->
<form data-action="submit-report">
```

### Textarea Actions

```html
<!-- Auto-growing Textarea -->
<!-- OLD -->
<textarea oninput="autoGrow(this)" onkeydown="handleEditorKeydown(event)"></textarea>

<!-- NEW -->
<textarea data-auto-grow
          id="post-content-input"></textarea>
<!-- Note: Keydown handler is automatically attached to #post-content-input -->
```

## Special Cases

### Escape HTML in Razor
When passing string data that might contain quotes or special characters:

```html
<!-- Use Razor's @ syntax carefully -->
<button data-action="quote-post"
        data-content="@Html.Raw(System.Web.HttpUtility.JavaScriptStringEncode(post.Content))">
    Quote
</button>

<!-- Or use data attributes for JSON -->
<button data-action="quote-post"
        data-post='@Json.Serialize(new { id = post.PublicId, content = post.Content })'>
    Quote
</button>
```

### Boolean Values
Always use lowercase strings for boolean data attributes:

```html
<!-- CORRECT -->
<button data-is-authenticated="@isAuthenticated.ToString().ToLower()">

<!-- INCORRECT -->
<button data-is-authenticated="@isAuthenticated">  <!-- Will be "True" not "true" -->
```

### Complex Objects
For complex data, use JSON encoding:

```html
<button data-action="load-post"
        data-post='@Json.Serialize(post)'>
    Load
</button>
```

## Migration Checklist

For each Razor Page file:

1. ☐ Search for `onclick=`
2. ☐ Replace with appropriate `data-action` and `data-*` attributes
3. ☐ Search for `onsubmit=`
4. ☐ Replace with `data-action` on the form
5. ☐ Search for `oninput=` (for textareas)
6. ☐ Replace with `data-auto-grow` attribute
7. ☐ Search for `onkeydown=`
8. ☐ Remove if it's for #post-content-input (handled automatically)
9. ☐ Test the page to ensure all actions work

## Files to Update

Primary files that need migration:

### High Priority
- `Pages/Discussions/Detail.cshtml` - Heavy onclick usage

### Medium Priority
- Any pages with buttons that call JavaScript functions
- Any forms with onsubmit handlers

### Low Priority
- Pages using only htmx attributes (hx-*)
- Pages with no JavaScript interactions

## Testing After Migration

1. Test each button/action to ensure it still works
2. Check browser console for errors
3. Verify data attributes are being read correctly
4. Test edge cases (special characters in content, etc.)

## Benefits of Migration

✅ **Cleaner HTML** - No JavaScript in markup
✅ **Better CSP compatibility** - No inline JavaScript
✅ **Easier testing** - Actions can be triggered programmatically
✅ **Better debugging** - All event handlers in one place
✅ **Consistent patterns** - Same approach across all pages

## Example: Complete Before/After for a Post Action Row

### Before
```html
<div class="post-actions">
    <button onclick="replyToPost('@post.PublicId', '@post.Author.DisplayName')">Reply</button>
    <button onclick="quotePost('@post.PublicId', `@post.Content`, '@post.Author.DisplayName')">Quote</button>
    <button onclick="editPost('@post.PublicId', '@Model.CurrentUserId')">Edit</button>
    <button onclick="toggleReactionPicker('@post.PublicId')">React</button>
</div>
```

### After
```html
<div class="post-actions">
    <button data-action="reply-to-post"
            data-post-id="@post.PublicId"
            data-author-name="@post.Author.DisplayName">
        Reply
    </button>
    <button data-action="quote-post"
            data-post-id="@post.PublicId"
            data-content="@Html.Raw(System.Web.HttpUtility.JavaScriptStringEncode(post.Content))"
            data-author-name="@post.Author.DisplayName">
        Quote
    </button>
    <button data-action="edit-post"
            data-post-id="@post.PublicId"
            data-user-id="@Model.CurrentUserId">
        Edit
    </button>
    <button data-action="toggle-reaction-picker"
            data-post-id="@post.PublicId">
        React
    </button>
</div>
```

## Notes

- The JavaScript event delegation is already in place in `discussion-detail.js`
- Legacy functions are kept for backwards compatibility during migration
- Once all pages are migrated, the legacy exports can be removed
- All new features should use the data-action pattern from the start
