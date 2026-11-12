# Gemini in Google Docs: insertion prompt

Open the intended Google Doc and use Ask Gemini. Google's current help describes working in the side panel or bottom bar, referencing files through Sources or @, and refining selected text. Availability depends on the account. Attach or paste the companion `FINAL-CONTROLLER-DOCUMENTATION.md` content; a local Windows path alone does not give Gemini access to it. If using a Drive copy, explicitly select that copy as a source.

Sources: [Using Google Workspace with Gemini](https://support.google.com/a/users/answer/15146419?hl=en) and [Collaborate with Gemini in Google Docs](https://support.google.com/docs/answer/14206696).

## Prompt to paste

Edit the open LP Final Submission document using the attached controller documentation as the evidence source. My contribution is the vehicle character controller, engine integration, camera and vehicle lab measurements for Zoom Zoom Delivery Co.

The PDF has older Rocket League material and a later section called “Hypothesis and Design Goals: Overcooked meets Crazy Taxi”. Use that later direction for the new material. Insert the new sections alongside it, using the document's existing heading styles and native tables. If the current document differs from the supplied PDF, locate the headings by their text rather than assuming page numbers still match.

Write the following sections in clear South African academic English:

1. Controller design goal and controller experience goal, explicitly labelled and kept separate. Explain how each connects driving to the delivery hypothesis and how it can be evaluated.
2. Unity engine and vehicle implementation, explaining the custom arcade controller, Rigidbody, four suspension rays, input handling, grounded techniques and automatic ratios in terms of their design purpose.
3. Vehicle lab measurement table, preserving the exact values and test conditions in the source. Label the results as recorded 26 October 2026 evidence, not a new test. Explain the 99%-of-top-speed definition, whole-car turning diameter and the specific 90-degree corner geometry.
4. Crazy Dash, Crazy Drift, Crazy Stop and quick-turn table, with input, conditions, effect and what each rewards. Do not invent monetary trick rewards.
5. Short Crazy Taxi research section distinguishing reconstructed behaviour, project adaptations and omitted systems. Explain why default Unity physics does not automatically reproduce authored arcade rules. Do not say physics engines did not exist or that Unity cannot recreate the feel.
6. Camera section covering follow behaviour, wall avoidance, near-plane clearance, drift framing, look-ahead and visibility limits around blind corners. Use current taxi settings, including 7 m look-ahead, rather than the old 28 m value.
7. Crash damage and recovery section. Include the implemented cargo-damage table and the separately labelled proposed R50 repair table. The R50 fee is a proposal, not present in the inspected implementation. State explicitly that choosing a threshold above 10 m/s is a proposed clarification of the draft's unresolved 8–10 m/s range. Do not claim it is approved. Keep the implemented five-second recovery penalty separate.
8. A short course connection and contribution paragraph using Bogost, Anthropy and Clark, and Heather Maxwell Chandler. Preserve source links. Do not invent quotations, page citations or an edition for the course reading.

Retain the source's distinction between implemented behaviour, recorded measurements, proposed design and untested experience claims. Leave teammates' unrelated work intact. Keep earlier controller material as dated history; flag contradictory current-tense summaries for correction rather than erasing the development record. Do not transform the proposed processing bays, stunt tips or repair fees into completed work. Do not manufacture lecturer feedback, user-test results or first-person reflections.

Preserve the uncertainty and remaining-work notes but remove the source file's editorial preamble and evidence-index path list from the main body; place relevant evidence in an appendix instead. Use actual citations for public sources and the provided lab evidence for numbers. Do not cite Gemini as evidence.

Finish by listing the inserted headings and any contradictions still requiring a human decision. In particular, identify whether repair fees replace or stack with cargo damage. Do not silently resolve that design decision.

## Review after insertion

Check that the two goals appear explicitly; all five measurements and conditions survived; current controls replaced old jump/flip claims only in current-design sections; proposed fees remain labelled; source links work; and tables are readable. A Gemini response is not proof that the document was edited—inspect the document itself after applying the suggested text.
