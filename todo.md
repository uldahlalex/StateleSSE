- hvordan fungerer scaffolding / code first med nuværende pg backplane?

- er serverid på sseconnection redundant?

- what is the backplane even doing with this new EF centric approach?

- pt er der ingen initiel data / singaturer på listen er blot Task

- http 1.1 skal have tls til 2.0
  - dette skal i docs

FK constraint on inserting message as anon
Connection id "0HNJGD0T0UM68", Request id "0HNJGD0T0UM68:00000001": An unhandled exception was thrown by the application.
Microsoft.EntityFrameworkCore.DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
---> Npgsql.PostgresException (0x80004005): 23503: insert or update on table "SseConnections" violates foreign key constraint "FK_SseConnections_Users_OwnerId"

- is it possible to make a client id-less system?
  - hvad gør vi fx med pokes?

- ws/sse conn for hvert endpoint?
  - det vil nok give meget bøvl 
    - det giver et mere endpoint baseret layout
    - ingen connection må begynde at blive stateful, for der skal vedligeholdes flere
    - problem: hver connection vil være et nyt connection ID og det duer ikke
    - vent lige: hvad med at hver connection repræsenterer queryen og ikke browseren?
      - hvordan vil man taget en given klient når hver browser har X conncetions? targeter man ud fra den åbne forbindelse til den konkrete feature?
- re-branding til EF.Core.Realtime?
