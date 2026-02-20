- hvordan fungerer scaffolding / code first med nuværende pg backplane?
- det skal være muligt også at FK mellem brugeren og connection med delete cascade
- er serverid på sseconnection redundant?

- is non periodic cleanup better? (now using on disconnect)

- er det bedre med user navigation property end ownerId string? hvad er egentlig bedst dx?


- ws/sse conn for hvert endpoint?
  - det vil nok give meget bøvl 
    - det giver et mere endpoint baseret layout
    - ingen connection må begynde at blive stateful, for der skal vedligeholdes flere
    - problem: hver connection vil være et nyt connection ID og det duer ikke
    - vent lige: hvad med at hver connection repræsenterer queryen og ikke browseren?
      - hvordan vil man taget en given klient når hver browser har X conncetions? targeter man ud fra den åbne forbindelse til den konkrete feature?
- re-branding til EF.Core.Realtime?
