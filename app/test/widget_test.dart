import 'package:canasta_cr/main.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('App launches', (tester) async {
    await tester.pumpWidget(const CanastaCRApp());
    expect(find.byType(CanastaCRApp), findsOneWidget);
  });
}
