# Esprit Solid Fixture

Extensión para ESPRIT EDGE 2026.2 destinada a convertir sólidos CAD de utillaje en accesorios de máquina.

El botón grande **Preparación de utillaje** se integra en la pestaña **Inicio**, junto a los comandos de configuración. Antes de abrir la ventana comprueba que el documento tenga una máquina cargada. Si no existe, pregunta si el usuario desea seleccionar una; al aceptar abre un selector en la carpeta de máquinas configurada por el cliente, carga el archivo `.mprj` y continúa automáticamente. Con una máquina válida activa la vista de pieza y abre una ventana flotante no modal.

La ventana sigue la identidad visual de ESPRIT y permite seleccionar un sólido de **Pieza** y uno o varios sólidos de **Utillaje**. El bruto dispone de tres modos: definirlo libremente en la configuración nativa de ESPRIT, elegir un sólido del documento o importar un archivo STL. Al pulsar un recuadro se activa la selección por ventana; la flecha lateral activa la captura individual limitada a sólidos. Las selecciones son acumulativas y un sólido se retira al volver a seleccionarlo, con `Supr` o mediante **Eliminar**.

Al aceptar, la extensión actualiza la primera pieza de trabajo y aplica el bruto elegido. En modo **Desde ESPRIT** abre la configuración de pieza, deja que el usuario cree el material con las herramientas nativas y reanuda el proceso al cerrar ese editor. Después genera los GDML y crea el montaje en árbol. Cuando los utillajes no se combinan, los ordena por su cota Z mínima: monta el más bajo sobre la máquina, cada accesorio siguiente sobre el anterior y la pieza sobre el último. Cuando se combinan, monta la pieza sobre el único accesorio generado.

La ventana se posiciona automáticamente en la esquina superior izquierda de ESPRIT, debajo de la cinta, y utiliza una cabecera con icono, acento verde y tipografía coherentes con ESPRIT EDGE.

El selector **Destino del montaje** permite trabajar en la configuración actual o crear una nueva. En una configuración nueva se selecciona automáticamente **Cara superior / Giro Y 180° + Z 180°** y se puede reutilizar la pieza de trabajo y el material resultante de la fase anterior. La nueva configuración conserva la máquina, elimina el montaje copiado y crea el nuevo árbol.

El tercer destino, **Nuevo archivo ESPRIT**, crea la siguiente fase en un documento independiente. El usuario selecciona la nueva máquina, puede editar el nombre propuesto `<Nombre>_Fase_<N>` y elegir otra carpeta de salida. La extensión calcula el material restante después de la última operación, lo guarda como pieza de trabajo nativa `.wp` junto al nuevo documento, protege el archivo original y convierte el documento activo en la copia de la nueva fase. En la copia elimina todas las operaciones, características, configuraciones adicionales y el montaje principal anteriores; después carga la máquina elegida, define el material restante como bruto y crea el nuevo montaje invertido. Si no se selecciona otra pieza, conserva la pieza ya definida.

Al crear una configuración adicional, el número de fase se obtiene del mayor recuento entre `MachineSetupChanges` y `MachineSetupSequences`, más uno. Por ejemplo, con una única configuración la siguiente es Fase 2; si ya existen Configuración 1 y Configuración 2, la siguiente es Fase 3. La extensión revisa también los ceros de trabajo de todas las configuraciones y asigna el primero libre desde G54. El desplazamiento incremental conserva X=0 e Y=0 y utiliza como Z la coordenada absoluta de la base de los accesorios: Z mínima en montaje normal y Z máxima original cuando el montaje está girado.

La salida utiliza la carpeta de accesorios configurada por cada cliente (`espFileTypeFixtures`) y crea dentro `Utillajes_<NombreCAD>`.

Si ya existe un GDML con el mismo nombre, se sobrescribe. También se eliminan las versiones numeradas anteriores generadas por la extensión (`_1`, `_2`, etc.) para evitar accesorios duplicados.

Al aceptar se genera un GDML independiente por cada sólido, con el nombre
`Utillaje_<NombreCAD>_Fase_<X>_Solido_<N>.gdml`. La geometría no se desplaza en X ni
en Y: solo se normaliza en Z respecto a la altura mínima del sólido. El adaptador de
accesorio `FA_1` se sitúa a la altura superior, mientras que el adaptador de pieza
`WA_1` recibe el mismo desplazamiento Z que la geometría para conservar el plano
original del conjunto tras normalizar el origen.

El botón **Combinar sólidos** permite crear un único GDML con toda la selección. En
ese modo, la referencia Z se sitúa en la base del sólido más bajo y `FA_1`
en la cota superior del sólido que alcanza la mayor altura.

El selector **Cara de montaje** permite elegir la cara inferior (Z normal) o la cara
superior. Los GDML conservan siempre la geometría original. En montaje superior la
extensión gira el accesorio raíz 180 grados sobre Y y después 180 grados sobre Z;
todo el árbol de utillajes y la pieza heredan la misma transformación rígida. No se
crean reflejos ni copias temporales. Después del giro se traslada el accesorio raíz
en Z positiva según la altura total del árbol, evitando que el montaje quede debajo
de la mesa. En modo combinado se toma la Z inferior del
conjunto para la geometría y la Z superior para `FA_1`; `WA_1` continúa en el plano
original mediante el mismo desplazamiento Z aplicado a la geometría.

Los utillajes de la lista se pueden retirar seleccionándolos y pulsando `Supr`, o mediante clic derecho y **Eliminar**.

El menú **Eliminar** muestra una X roja. Los sólidos añadidos permanecen seleccionados gráficamente; al seleccionarlos de nuevo durante la captura se desmarcan y se retiran de la lista.
