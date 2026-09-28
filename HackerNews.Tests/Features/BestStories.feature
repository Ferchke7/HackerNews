Feature: Best Stories Retrieval
  As an API consumer
  I want to retrieve the top N stories from Hacker News
  So that I can consume the highest rated content ordered by score

  Scenario: Retrieve top 2 stories successfully in descending score order
    Given the external Hacker News API contains stories:
      | Id  | Title           | Score | PostedBy |
      | 101 | Low Score Story | 50    | user_a   |
      | 102 | Top Story       | 800   | user_b   |
      | 103 | Mid Story       | 300   | user_c   |
    When I request the best 2 stories
    Then the response status code should be 200
    And the returned stories should have scores in order:
      | Score |
      | 800   |
      | 300   |

  Scenario: Request stories with invalid non-positive parameter N
    When I request the best -3 stories
    Then the response status code should be 400
    And the error response should have code "S101"

  Scenario: Request stories without specifying parameter N
    When I request best stories without parameter N
    Then the response status code should be 400
    And the error response should have code "S102"

  Scenario: Deleted and dead stories are excluded from results
    Given the external Hacker News API contains complex items:
      | Id  | Title         | Score | PostedBy | Deleted | Dead  | Type  |
      | 201 | Active High   | 900   | user_a   | false   | false | story |
      | 202 | Deleted Story | 950   | user_b   | true    | false | story |
      | 203 | Dead Story    | 850   | user_c   | false   | true  | story |
      | 204 | Active Low    | 400   | user_d   | false   | false | story |
    When I request the best 2 stories
    Then the response status code should be 200
    And the returned stories should have scores in order:
      | Score |
      | 900   |
      | 400   |

  Scenario: Non-story item types such as jobs and polls are excluded
    Given the external Hacker News API contains complex items:
      | Id  | Title       | Score | PostedBy | Deleted | Dead  | Type  |
      | 301 | Job Post    | 1000  | company  | false   | false | job   |
      | 302 | Poll Post   | 800   | pollster | false   | false | poll  |
      | 303 | Real Story  | 500   | user_e   | false   | false | story |
    When I request the best 1 stories
    Then the response status code should be 200
    And the returned stories should have scores in order:
      | Score |
      | 500   |

  Scenario: Story without external URL preserves null uri
    Given the external Hacker News API contains an Ask HN story with id 401 and score 600
    When I request the best 1 stories
    Then the response status code should be 200
    And story 0 should have null uri

  Scenario: Requesting more stories than available returns all available stories
    Given the external Hacker News API contains stories:
      | Id  | Title   | Score | PostedBy |
      | 501 | Story 1 | 300   | user_1   |
      | 502 | Story 2 | 200   | user_2   |
    When I request the best 10 stories
    Then the response status code should be 200
    And exactly 2 stories should be returned

  Scenario: Complete JSON schema matches specification
    Given the external Hacker News API contains a sample story matching the specification:
      | Field        | Value                                                         |
      | Title        | A uBlock Origin update was rejected from the Chrome Web Store |
      | Uri          | https://github.com/uBlockOrigin/uBlock-issues/issues/745      |
      | PostedBy     | ismaildonmez                                                  |
      | Time         | 1570887781                                                    |
      | Score        | 1716                                                          |
      | CommentCount | 572                                                           |
    When I request the best 1 stories
    Then the response status code should be 200
    And story 0 should strictly match the specification fields

  Scenario: Upstream Hacker News API is unavailable returns service unavailable
    Given the external Hacker News API is unavailable
    When I request the best 5 stories
    Then the response status code should be 503
    And the error response should have code "S201"

  Scenario: Subsequent requests are served consistently from cache
    Given the external Hacker News API contains stories:
      | Id  | Title          | Score | PostedBy |
      | 601 | Cached Story 1 | 700   | author_1 |
      | 602 | Cached Story 2 | 650   | author_2 |
    When I request the best 2 stories
    Then the response status code should be 200
    And exactly 2 stories should be returned
    When I request the best 2 stories
    Then the response status code should be 200
    And exactly 2 stories should be returned
