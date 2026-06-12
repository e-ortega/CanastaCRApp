import 'package:flutter_test/flutter_test.dart';
import 'package:canasta_cr/core/api/api_client.dart';

void main() {
  group('ApiClient', () {
    test('can be instantiated', () {
      final client = ApiClient();
      expect(client, isNotNull);
    });
  });
}
