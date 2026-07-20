# Filterable

Test a design idea for a filterable pattern.

* Data comes in off the wire and is converted to messages.
* The messages are added to a data bus
* The data bus has registed decorators
* A subscriber can subscribe to a data stream

## Example
1. UDP data packet comes in. 
2. It is parsed into one of three messages
3. Those messages are added to the bus
4. Each of those messages have a specific Filterable decoractor associated
5. The messages are decorated
6. A subscriber is listening for a IFilterable, and thus should get all the decorated messages
