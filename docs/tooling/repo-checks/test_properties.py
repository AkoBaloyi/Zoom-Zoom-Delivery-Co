"""Property-based checks for repository configuration."""

from hypothesis import given, settings, strategies as st

from generators import ignored_path, lfs_path, project_path


# Feature: unity-team-repo-setup, Property 1: Every excluded path is ignored
# **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
@settings(max_examples=100, deadline=None)
@given(paths=st.lists(ignored_path(), min_size=1, max_size=50))
def test_every_excluded_path_is_ignored(repo_git, paths: list[str]) -> None:
    """Require one batched Git query to report every excluded path ignored."""

    assert repo_git.check_ignore(paths) == frozenset(paths)


# Feature: unity-team-repo-setup, Property 2: No project path is ever ignored
# **Validates: Requirements 1.3**
@settings(max_examples=100, deadline=None)
@given(paths=st.lists(project_path(), min_size=1, max_size=50))
def test_no_project_path_is_ever_ignored(repo_git, paths: list[str]) -> None:
    """Require one batched Git query to leave every project path unignored."""

    assert repo_git.check_ignore(paths) == frozenset()


# Feature: unity-team-repo-setup, Property 3: Binary extensions route through Git LFS in either case
# **Validates: Requirements 3.2, 3.4**
@settings(max_examples=100, deadline=None)
@given(paths=st.lists(lfs_path(), min_size=1, max_size=50))
def test_binary_extensions_route_through_git_lfs_in_either_case(
    repo_git, paths: list[str]
) -> None:
    """Require one batched Git query to route every binary path through LFS."""

    attributes = repo_git.check_attr(paths, ("filter", "diff", "merge", "text"))
    expected = {"filter": "lfs", "diff": "lfs", "merge": "lfs", "text": "unset"}

    for path in paths:
        assert attributes[path] == expected
