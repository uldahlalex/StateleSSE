import { test, expect } from '@playwright/test';

test.describe('Chat SSE functionality', () => {
  test('should receive and display messages when clicking Add button', async ({ page }) => {
    await page.goto('/');

    await page.waitForLoadState('domcontentloaded');

    const initialConsoleMessages: string[] = [];
    page.on('console', msg => {
      const text = msg.text();
      initialConsoleMessages.push(text);
      console.log('Browser console:', text);
    });

    await page.waitForTimeout(2000);

    const sseOpenMessage = initialConsoleMessages.find(msg => msg.includes('SSE connection opened'));
    expect(sseOpenMessage).toBeTruthy();
    console.log('✓ SSE connection opened');

    const initialText = await page.locator('body').textContent();
    console.log('Initial page content:', initialText);

    console.log('Clicking Add button to send message...');
    await page.click('button:has-text("Add")');

    await page.waitForTimeout(3000);

    const receivedMessage = initialConsoleMessages.find(msg => msg.includes('Received message:'));
    if (receivedMessage) {
      console.log('✓ Message received in console:', receivedMessage);
    } else {
      console.log('✗ No message received in console');
    }

    const finalText = await page.locator('body').textContent();
    console.log('Final page content:', finalText);

    const hasMessage = finalText?.includes('"content":"hi"') || finalText?.includes('hi');

    if (hasMessage) {
      console.log('✓ SUCCESS: Message appears on page!');
    } else {
      console.log('✗ FAILURE: Message does not appear on page');
      console.log('All console messages:', initialConsoleMessages);
    }

    expect(hasMessage).toBeTruthy();
  });
});
