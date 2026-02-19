
- ws conn for hvert endpoint?
  - sse er begrænset til 6, så der kører vi single stream
  - det giver et mere endpoint baseret layout
  - ingen connection må begynde at blive stateful, for der skal vedligeholdes flere
  - problem: hver connection vil være et nyt connection ID og det duer ikke
- re-branding til EF.Core.Realtime?
- hvordan fungerer scaffolding / code first med nuværende pg backplane? 
