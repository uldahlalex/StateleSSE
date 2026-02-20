- hvordan fungerer scaffolding / code first med nuværende pg backplane?
  - hvis man kan få dbsets<T> for connection tables vil det være fordelagtigt
  - det skal være muligt også at FK mellem brugeren og connection med delete cascade 

- listen/notify driver disse features:
  - db state change pushing
    - hvis man vil sacrifice horizontal scaling kan dette gøres uden
  - connection management
    - kan man lave connectoin management uden listen/notify og bare bruge heartbeat / pingpong systemet til at rydde persisteret data op?
  - hvad med et helt npgsql-løst setup?

- ws conn for hvert endpoint?
  - sse er begrænset til 6, så der kører vi single stream
  - det giver et mere endpoint baseret layout
  - ingen connection må begynde at blive stateful, for der skal vedligeholdes flere
  - problem: hver connection vil være et nyt connection ID og det duer ikke
- re-branding til EF.Core.Realtime?
